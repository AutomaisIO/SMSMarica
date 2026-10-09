using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Sandbox.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Sandbox;

/// <summary>
/// "Entrar como paciente" (Sandbox de QA). O operador escolhe um paciente aqui; enquanto valer,
/// quem entra no app do cidadão com o CPF DO OPERADOR — e recebe o código no WhatsApp verificado
/// DO OPERADOR — abre a sessão como esse paciente. O paciente não é avisado nem perde a sessão.
/// <para>A prova de quem está entrando continua sendo o código no WhatsApp: por isso só fica apto
/// quem tem CPF no usuário e esse CPF tem contato verificado no hub. Sem isso, qualquer um que
/// soubesse o CPF do operador entraria como o paciente.</para>
/// </summary>
public interface IPersonificacaoPacienteService
{
    /// <summary>Se o operador logado pode usar e qual paciente está valendo para ele.</summary>
    Task<PersonificacaoStatusDto> ObterStatusAsync(CancellationToken ct = default);

    /// <summary>Passa a abrir o app como o paciente. Substitui a anterior (e derruba as sessões dela).</summary>
    Task<PersonificacaoStatusDto> AtivarAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>Volta ao normal: o CPF do operador abre o app como ele mesmo; sessões abertas caem.</summary>
    Task EncerrarAsync(CancellationToken ct = default);

    /// <summary>
    /// Login do app: o CPF que acabou de confirmar o código é de um operador com personificação
    /// valendo? Devolve o paciente a abrir; null = login normal.
    /// </summary>
    Task<PersonificacaoAlvo?> ResolverNoLoginAsync(string cpf, CancellationToken ct = default);
}

/// <summary>Paciente que a sessão vai abrir no lugar do operador.</summary>
public sealed record PersonificacaoAlvo(
    Guid PersonificacaoId, Guid UsuarioId, Guid PacienteId, string Nome, string Cpf, DateTime ExpiraEm);

public sealed class PersonificacaoPacienteService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IIdentidadeService identidade,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<PersonificacaoPacienteService> logger) : IPersonificacaoPacienteService
{
    /// <summary>Um dia de trabalho. Esquecer ligado não pode deixar o CPF do operador abrindo
    /// outra pessoa para sempre.</summary>
    internal static readonly TimeSpan Validade = TimeSpan.FromHours(12);

    public async Task<PersonificacaoStatusDto> ObterStatusAsync(CancellationToken ct = default)
    {
        var usuarioId = UsuarioLogado();
        var operador = await ConferirOperadorAsync(usuarioId, ct);
        return await MontarStatusAsync(usuarioId, operador, ct);
    }

    public async Task<PersonificacaoStatusDto> AtivarAsync(Guid pacienteId, CancellationToken ct = default)
    {
        var usuarioId = UsuarioLogado();
        var operador = await ConferirOperadorAsync(usuarioId, ct);
        if (operador.Motivo is { } motivo)
            throw new ConflitoException("personificacao.inapta", motivo);

        var alvo = await pacientes.ObterPorIdAsync(pacienteId, ct);
        if (Digitos(alvo.Cpf).Length != 11)
            throw new ConflitoException(
                "personificacao.sem_cpf", "Escolha um paciente com CPF — a sessão do app é identificada pelo CPF.");
        if (!alvo.Ativo)
            throw new ConflitoException("personificacao.inativo", "O cadastro deste paciente está inativo.");
        if (alvo.Id == operador.PacienteId)
            throw new ValidacaoException(
                "personificacao.proprio", "Este é o seu próprio cadastro — para entrar como você, basta encerrar.");

        var agora = DateTime.UtcNow;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // ORDEM OBRIGATÓRIA: a anterior fecha ANTES de a nova nascer — o índice único só
            // admite uma em aberto por operador.
            await EncerrarAbertasAsync(usuarioId, agora, ct);
            db.PersonificacoesPaciente.Add(new PersonificacaoPaciente
            {
                Id = Guid.CreateVersion7(),
                UsuarioId = usuarioId,
                PatientId = alvo.Id,
                CriadaEm = agora,
                ExpiraEm = agora.Add(Validade),
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        logger.LogWarning(
            "Personificação ativada: o usuário {UsuarioId} passa a entrar no app como o paciente {PacienteId}.",
            usuarioId, alvo.Id);
        return await MontarStatusAsync(usuarioId, operador, ct);
    }

    public async Task EncerrarAsync(CancellationToken ct = default)
    {
        var usuarioId = UsuarioLogado();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await EncerrarAbertasAsync(usuarioId, DateTime.UtcNow, ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task<PersonificacaoAlvo?> ResolverNoLoginAsync(string cpf, CancellationToken ct = default)
    {
        var digitos = Digitos(cpf);
        if (digitos.Length != 11) return null;

        var agora = DateTime.UtcNow;
        var p = await db.PersonificacoesPaciente.AsNoTracking()
            .Where(x => x.EncerradaEm == null && x.ExpiraEm > agora
                && x.Usuario.Cpf == digitos && x.Usuario.Ativo && x.Usuario.ExcluidoEm == null)
            .Select(x => new { x.Id, x.UsuarioId, x.PatientId, x.ExpiraEm })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        // Quem perdeu o Sandbox depois de ativar não entra mais como ninguém.
        if (!await TemSandboxEdicaoAsync(p.UsuarioId, ct))
        {
            logger.LogWarning(
                "Personificação {Id} ignorada no login: o usuário {UsuarioId} não tem mais o Sandbox.",
                p.Id, p.UsuarioId);
            return null;
        }

        var alvo = await pacientes.ObterPorIdAsync(p.PatientId, ct);
        var cpfAlvo = Digitos(alvo.Cpf);
        if (cpfAlvo.Length != 11 || !alvo.Ativo) return null;

        logger.LogWarning(
            "Personificação {Id}: o usuário {UsuarioId} entrou no app como o paciente {PacienteId}.",
            p.Id, p.UsuarioId, p.PatientId);
        return new PersonificacaoAlvo(p.Id, p.UsuarioId, p.PatientId, alvo.NomeCompleto, cpfAlvo, p.ExpiraEm);
    }

    /// <summary>Fecha as abertas do operador (inclusive as já expiradas, que ainda ocupam o índice)
    /// e derruba as sessões do app que nasceram delas.</summary>
    private async Task EncerrarAbertasAsync(Guid usuarioId, DateTime agora, CancellationToken ct)
    {
        var abertas = await db.PersonificacoesPaciente
            .Where(x => x.UsuarioId == usuarioId && x.EncerradaEm == null)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (abertas.Count == 0) return;

        await db.CidadaoSessoes
            .Where(s => s.PersonificacaoId != null && abertas.Contains(s.PersonificacaoId.Value) && s.RevogadaEm == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevogadaEm, agora), ct);
        await db.PersonificacoesPaciente
            .Where(x => abertas.Contains(x.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.EncerradaEm, agora), ct);
    }

    private async Task<PersonificacaoStatusDto> MontarStatusAsync(
        Guid usuarioId, OperadorConferido operador, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var ativa = await db.PersonificacoesPaciente.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId && x.EncerradaEm == null && x.ExpiraEm > agora)
            .Select(x => new
            {
                x.PatientId,
                x.CriadaEm,
                x.ExpiraEm,
                Sessoes = db.CidadaoSessoes.Count(s => s.PersonificacaoId == x.Id && s.RevogadaEm == null && s.ExpiraEm > agora),
            })
            .FirstOrDefaultAsync(ct);

        PersonificacaoAtivaDto? dto = null;
        if (ativa is not null)
        {
            var alvo = await pacientes.ObterPorIdAsync(ativa.PatientId, ct);
            dto = new PersonificacaoAtivaDto(ativa.PatientId, alvo.NomeCompleto, ativa.CriadaEm, ativa.ExpiraEm, ativa.Sessoes);
        }

        return new PersonificacaoStatusDto(
            operador.Motivo is null, operador.Motivo, MascararCpf(operador.Cpf), operador.TelefoneMascarado, dto);
    }

    /// <summary>
    /// O operador só fica apto com o MESMO caminho que o app vai exigir no login: CPF no usuário →
    /// cadastro de paciente ativo com esse CPF → WhatsApp verificado. É para esse número que o
    /// código vai, e é ele que prova que quem está no app é o operador.
    /// </summary>
    private async Task<OperadorConferido> ConferirOperadorAsync(Guid usuarioId, CancellationToken ct)
    {
        var cpf = Digitos(await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Cpf)
            .FirstOrDefaultAsync(ct));
        if (cpf.Length != 11)
            return new(null, null, null,
                "O seu usuário não tem CPF cadastrado. Peça a quem administra os usuários para preencher e volte aqui.");

        var existe = await pacientes.ObterPorCpfAsync(cpf, ct);
        if (existe is null)
            return new(cpf, null, null,
                "O seu CPF ainda não tem cadastro de paciente. Entre uma vez no app do cidadão com o seu CPF — "
                + "o app cria o cadastro e verifica o seu WhatsApp — e volte aqui.");
        if (!existe.Ativo)
            return new(cpf, existe.Id, null, "O seu cadastro de paciente está inativo.");

        var dados = await pacientes.ObterPorIdAsync(existe.Id, ct);
        if (string.IsNullOrWhiteSpace(dados.TelefoneVerificado))
            return new(cpf, existe.Id, null,
                "O seu WhatsApp ainda não está verificado. Entre uma vez no app do cidadão com o seu CPF — "
                + "o código verifica o número — e volte aqui.");

        return new(cpf, existe.Id, MascararTelefone(dados.TelefoneVerificado), null);
    }

    private async Task<bool> TemSandboxEdicaoAsync(Guid usuarioId, CancellationToken ct)
    {
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId, ct);
            return perms.Resolvidas.Any(p => p.Modulo == ModuloPermissao.Sandbox
                && (p.Acoes & AcoesPermissao.Edicao) == AcoesPermissao.Edicao);
        }
        catch (NaoEncontradoException) { return false; }
    }

    private Guid UsuarioLogado() =>
        usuarioAtual.UsuarioId ?? throw new ValidacaoException("operador", "Operador não identificado na requisição.");

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    private static string? MascararCpf(string? cpf) =>
        cpf is { Length: 11 } ? $"***.{cpf[3..6]}.{cpf[6..9]}-**" : null;

    private static string? MascararTelefone(string telefone)
    {
        var d = Digitos(telefone);
        return d.Length < 4 ? null : "***-" + d[^4..];
    }

    /// <param name="Motivo">null = apto.</param>
    private sealed record OperadorConferido(string? Cpf, Guid? PacienteId, string? TelefoneMascarado, string? Motivo);
}
