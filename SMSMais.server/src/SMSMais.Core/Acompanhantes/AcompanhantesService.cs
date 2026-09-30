using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Acompanhantes.Dtos;
using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Acompanhantes;

/// <summary>
/// Acompanhantes do paciente no Transporte de Pacientes. A lista é do paciente (vale para todos os
/// atendimentos dele); a equipe cadastra pelo painel e o próprio paciente pelo app.
/// </summary>
public interface IAcompanhantesService
{
    Task<IReadOnlyList<AcompanhanteDto>> ListarDoPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confere o par CPF + nascimento e devolve o nome, para quem cadastra confirmar antes. Olha
    /// primeiro a base de pacientes; se não bater, pergunta ao proxy de CPF (Receita/CADSUS).
    /// </summary>
    Task<AcompanhanteConsultaDto> ConsultarAsync(Guid pacienteId, ConsultarAcompanhanteRequest request, CancellationToken cancellationToken = default);

    Task<AcompanhanteDto> AdicionarAsync(Guid pacienteId, AdicionarAcompanhanteRequest request, OrigemCadastroAcompanhante origem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tira da lista (exclusão lógica). Quem remove pelo app só tira quem cadastrou pelo app; ninguém
    /// sai da lista enquanto estiver escolhido para uma viagem futura.
    /// </summary>
    Task RemoverAsync(Guid pacienteId, Guid acompanhanteId, OrigemCadastroAcompanhante quemRemove, CancellationToken cancellationToken = default);
}

public sealed class AcompanhantesService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IPacienteResolver resolver,
    IConsultaCpfService consultaCpf,
    IMemoryCache cache,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<AcompanhantesService> logger) : IAcompanhantesService
{
    /// <summary>A confirmação vem logo depois da consulta: guardar o resultado evita pagar a consulta
    /// da Receita duas vezes pela mesma pessoa.</summary>
    private static readonly TimeSpan ValidadeConferencia = TimeSpan.FromMinutes(15);

    private sealed record PessoaConferida(string Nome, FonteNomeAcompanhante Fonte, Guid? PacienteVinculadoId);

    public async Task<IReadOnlyList<AcompanhanteDto>> ListarDoPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var lista = await db.Acompanhantes.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId && a.ExcluidoEm == null)
            .OrderBy(a => a.Nome)
            .ToListAsync(cancellationToken);
        return [.. lista.Select(ParaDto)];
    }

    public async Task<AcompanhanteConsultaDto> ConsultarAsync(
        Guid pacienteId, ConsultarAcompanhanteRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = CpfValido(request.Cpf);
        GarantirNascimento(request.DataNascimento);
        await GarantirQueNaoEOProprioPacienteAsync(pacienteId, cpf, cancellationToken);

        var existente = await db.Acompanhantes.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PacienteId == pacienteId && a.Cpf == cpf && a.ExcluidoEm == null, cancellationToken);
        if (existente is not null && existente.DataNascimento == request.DataNascimento)
        {
            return new AcompanhanteConsultaDto(cpf, existente.Nome, existente.DataNascimento, JaCadastrado: true);
        }

        var pessoa = await ConferirAsync(pacienteId, cpf, request.DataNascimento, cancellationToken);
        return new AcompanhanteConsultaDto(cpf, pessoa.Nome, request.DataNascimento, JaCadastrado: existente is not null);
    }

    public async Task<AcompanhanteDto> AdicionarAsync(
        Guid pacienteId, AdicionarAcompanhanteRequest request, OrigemCadastroAcompanhante origem, CancellationToken cancellationToken = default)
    {
        var cpf = CpfValido(request.Cpf);
        GarantirNascimento(request.DataNascimento);
        await GarantirQueNaoEOProprioPacienteAsync(pacienteId, cpf, cancellationToken);

        if (await db.Acompanhantes.AsNoTracking()
                .AnyAsync(a => a.PacienteId == pacienteId && a.Cpf == cpf && a.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("acompanhante.ja_cadastrado", "Essa pessoa já está na lista de acompanhantes.");
        }

        var pessoa = await ConferirAsync(pacienteId, cpf, request.DataNascimento, cancellationToken);
        var telefone = CpfBr.SoDigitos(request.Telefone);

        var acompanhante = new Acompanhante
        {
            Id = Guid.CreateVersion7(),
            PacienteId = pacienteId,
            Cpf = cpf,
            Nome = pessoa.Nome,
            DataNascimento = request.DataNascimento,
            Parentesco = request.Parentesco,
            Telefone = telefone.Length == 0 ? null : telefone,
            PacienteVinculadoId = pessoa.PacienteVinculadoId,
            FonteNome = pessoa.Fonte,
            Origem = origem,
            CriadoEm = DateTime.UtcNow,
            // Pelo app não há usuário do painel: o registro de que foi o paciente é a origem.
            CriadoPor = origem == OrigemCadastroAcompanhante.Painel ? usuarioAtual.UsuarioId : null,
        };

        db.Acompanhantes.Add(acompanhante);
        await db.SaveChangesAsync(cancellationToken);
        return ParaDto(acompanhante);
    }

    public async Task RemoverAsync(
        Guid pacienteId, Guid acompanhanteId, OrigemCadastroAcompanhante quemRemove, CancellationToken cancellationToken = default)
    {
        var acompanhante = await db.Acompanhantes
            .FirstOrDefaultAsync(a => a.Id == acompanhanteId && a.PacienteId == pacienteId && a.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Acompanhante), acompanhanteId);

        if (quemRemove == OrigemCadastroAcompanhante.App && acompanhante.Origem != OrigemCadastroAcompanhante.App)
        {
            throw new ConflitoException("acompanhante.cadastrado_pela_equipe",
                "Esse acompanhante foi cadastrado pela equipe do transporte. Para tirar, fale com a equipe.");
        }

        var hoje = FusoBrasilia.HojeEmBrasilia();
        var emViagemFutura = await db.SessoesAcompanhantes.AsNoTracking()
            .AnyAsync(x => x.AcompanhanteId == acompanhanteId
                           && x.Sessao!.DataPrevista >= hoje
                           && x.Sessao.Status != StatusSessao.Realizada
                           && x.Sessao.Status != StatusSessao.Cancelada
                           && x.Sessao.Status != StatusSessao.NaoRealizada, cancellationToken);
        if (emViagemFutura)
        {
            throw new ConflitoException("acompanhante.em_viagem_futura",
                "Essa pessoa está escolhida para uma viagem que ainda vai acontecer. Tire-a da viagem antes de remover.");
        }

        acompanhante.ExcluidoEm = DateTime.UtcNow;
        acompanhante.ExcluidoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Quem é a dona do CPF, conferida pelo nascimento. A base de pacientes vem primeiro (não custa
    /// nada); o proxy de CPF só quando a base não confirma. O nome nunca é o que alguém digitou.
    /// </summary>
    private async Task<PessoaConferida> ConferirAsync(Guid pacienteId, string cpf, DateOnly nascimento, CancellationToken ct)
    {
        var chave = $"acompanhante:conferencia:{cpf}:{nascimento:yyyyMMdd}";
        if (cache.TryGetValue<PessoaConferida>(chave, out var guardada) && guardada is not null)
        {
            return guardada;
        }

        logger.LogInformation("Acompanhante: conferindo CPF ***{Final} para o paciente {Paciente}.", cpf[^4..], pacienteId);

        PacienteExistenciaDto? naBase = null;
        try
        {
            naBase = await pacientes.ObterPorCpfAsync(cpf, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Base fora do ar não impede: o proxy confere sozinho.
            logger.LogWarning(ex, "Acompanhante: base de pacientes indisponível na conferência do CPF ***{Final}.", cpf[^4..]);
        }

        PessoaConferida pessoa;
        var resumo = naBase is null ? null : await resolver.ResolverAsync(naBase.Id, ct);
        if (naBase is not null && resumo?.DataNascimento == nascimento && !string.IsNullOrWhiteSpace(naBase.NomeCompleto))
        {
            pessoa = new PessoaConferida(naBase.NomeCompleto, FonteNomeAcompanhante.Base, naBase.Id);
        }
        else
        {
            var nome = await ConsultarProxyAsync(cpf, nascimento, ct);
            // O CPF é a identidade: se ele existe na base, é essa pessoa, mesmo com o nascimento de
            // lá diferente (o proxy acabou de confirmar o par).
            pessoa = new PessoaConferida(nome, FonteNomeAcompanhante.ConsultaCpf, naBase?.Id);
        }

        cache.Set(chave, pessoa, ValidadeConferencia);
        return pessoa;
    }

    private async Task<string> ConsultarProxyAsync(string cpf, DateOnly nascimento, CancellationToken ct)
    {
        try
        {
            var resposta = await consultaCpf.ConsultarCpfAsync(cpf, nascimento, ct);
            if (!string.IsNullOrWhiteSpace(resposta.Nome)) return resposta.Nome.Trim();
        }
        catch (ValidacaoException ex)
        {
            // Negativa do motor: o par não bate. A mensagem do proxy é escrita para o operador; esta
            // serve ao painel e ao app, e não diz qual dos dois dados está errado.
            logger.LogInformation("Acompanhante: CPF ***{Final} não confere com o nascimento ({Msg}).", cpf[^4..], ex.Message);
        }
        catch (ConflitoException ex)
        {
            logger.LogWarning("Acompanhante: consulta de CPF indisponível ({Msg}).", ex.Message);
            throw new ConflitoException("acompanhante.consulta_indisponivel",
                "Não foi possível conferir o CPF agora. Tente de novo em alguns minutos.");
        }

        throw new ValidacaoException("acompanhante.nao_confere",
            "Não encontramos essa pessoa com esse CPF e essa data de nascimento. Confira os dados.");
    }

    private async Task GarantirQueNaoEOProprioPacienteAsync(Guid pacienteId, string cpf, CancellationToken ct)
    {
        var paciente = await resolver.ResolverAsync(pacienteId, ct);
        if (paciente?.Cpf is { } cpfPaciente && CpfBr.SoDigitos(cpfPaciente) == cpf)
        {
            throw new ValidacaoException("acompanhante.proprio_paciente",
                "O acompanhante não pode ser o próprio paciente.");
        }
    }

    private static string CpfValido(string? cpf)
    {
        if (!CpfBr.EhValido(cpf))
        {
            throw new ValidacaoException("acompanhante.cpf_invalido", "CPF inválido. Confira os números.");
        }
        return CpfBr.SoDigitos(cpf);
    }

    private static void GarantirNascimento(DateOnly nascimento)
    {
        if (nascimento.Year < 1900 || nascimento > FusoBrasilia.HojeEmBrasilia())
        {
            throw new ValidacaoException("acompanhante.nascimento_invalido", "Data de nascimento inválida.");
        }
    }

    private static AcompanhanteDto ParaDto(Acompanhante a) => new(
        a.Id, a.PacienteId, a.Cpf, a.Nome, a.DataNascimento, a.Parentesco, a.Telefone,
        a.PacienteVinculadoId is not null, a.Origem, a.CriadoEm);
}
