using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>Resultado de <see cref="ICompletadorFichaSemCpf.CompletarAsync"/>: o paciente a usar
/// na marcação (a própria ficha, completada — ou o dono do CPF, quando ele já existia) e a frase
/// para a trilha de passos da importação (null = nada digno de nota).</summary>
public sealed record CompletudeResultado(Guid PacienteId, string? Nome, string? Passo);

/// <summary>
/// Ficha achada por CNS mas SEM CPF não é ponto final. Era: a importação reusava a ficha
/// incompleta e seguia — e o desafio de verificação por WhatsApp perguntava um CPF que a ficha
/// não tinha (caso Marcia, 28/09/2026: respondeu o próprio CPF, correto, e queimou as chances
/// contra um campo vazio). Pior, cada fonte que só traz um identificador (SER→CNS,
/// Klinikos→CPF) plantava metade da pessoa, e as metades nunca se encontravam.
/// </summary>
public interface ICompletadorFichaSemCpf
{
    /// <summary>
    /// Tenta completar a ficha pelo CADSUS (porta configurada). CPF livre → carimba na própria
    /// ficha (+ nascimento, se faltava). CPF já de outro cadastro → o CNS é absorvido pelo dono
    /// e a marcação passa a usá-lo (mesma decisão do fluxo da recepção,
    /// <c>DefinirCpfDoPacienteAsync</c>); a sombra fica para a fusão. Nunca derruba a importação:
    /// completar é bônus, e qualquer falha devolve a ficha original.
    /// </summary>
    Task<CompletudeResultado> CompletarAsync(
        PacienteExistenciaDto porCns, string cns, CancellationToken ct = default);

    /// <summary>Redirecionamento memorizado deste CNS (desfecho "repontado"), sem consulta nova.
    /// Null quando não há memória ou o destino sumiu.</summary>
    Task<PacienteExistenciaDto?> DestinoMemorizadoAsync(string cns, CancellationToken ct = default);

    /// <summary>Registra que o dono deste CNS é <paramref name="dono"/>: absorve o CNS no
    /// cadastro dele (idempotente) e memoriza o redirecionamento para as próximas varreduras.</summary>
    Task RegistrarDonoDoCnsAsync(string cns, PacienteExistenciaDto dono, CancellationToken ct = default);
}

public sealed class CompletadorFichaSemCpf(
    SmsMaisDbContext db,
    ICadastroPacienteService cadastro,
    IPacientesService pacientes,
    Auditoria.IAuditoriaService auditoria,
    ILogger<CompletadorFichaSemCpf> logger) : ICompletadorFichaSemCpf
{
    /// <summary>Teto de consultas de completude por escopo (uma execução de importação): o lote
    /// grande converge ao longo das varreduras diárias em vez de estourar a fonte de uma vez.
    /// O serviço é scoped, como o runner de importação — o contador zera a cada lote.</summary>
    private const int MaxConsultasPorEscopo = 25;

    /// <summary>SemFicha/SemCpf não são permanentes (CNS provisório regulariza, CPF chega ao
    /// CADSUS depois) — revalida, mas devagar.</summary>
    private static readonly TimeSpan Revalidacao = TimeSpan.FromDays(90);

    private int _consultas;

    public async Task<CompletudeResultado> CompletarAsync(
        PacienteExistenciaDto porCns, string cns, CancellationToken ct = default)
    {
        var manter = new CompletudeResultado(porCns.Id, porCns.NomeCompleto, null);
        var digitos = SoDigitos(cns);
        if (!string.IsNullOrWhiteSpace(porCns.Cpf) || digitos.Length != 15) return manter;

        // A memória evita pagar a consulta de novo — e é ela que segura o desfecho "repontado":
        // a sombra continua sendo achada por CNS nas varreduras seguintes.
        var memoria = await db.CadsusCompletudes.FindAsync([digitos], ct);
        if (memoria is { Desfecho: DesfechoCadsusCompletude.RepontadoParaExistente, PacienteDestinoId: { } destinoId })
        {
            if (await ObterExistenciaAsync(destinoId, ct) is { } destino)
            {
                return new CompletudeResultado(destino.Id, destino.NomeCompleto,
                    $"Ficha sem CPF: CNS já resolvido para o cadastro de {destino.NomeCompleto} (memória do CADSUS).");
            }
            // Destino sumiu (fusão/exclusão): cai para a reconsulta, que sobrescreve a memória.
        }
        else if (memoria is not null && DateTime.UtcNow - memoria.ConsultadoEm < Revalidacao)
            return manter; // SemFicha/SemCpf recente: nada mudou desde a última pergunta

        if (_consultas >= MaxConsultasPorEscopo) return manter; // converge nas próximas varreduras
        _consultas++;

        ConsultaCnsRespostaDto cadsus;
        try
        {
            cadsus = await cadastro.ConsultarPorCnsAsync(digitos, ct);
        }
        catch (NaoEncontradoException)
        {
            await MemorizarAsync(memoria, digitos, DesfechoCadsusCompletude.SemFicha, null, ct);
            return manter with { Passo = "Ficha sem CPF: CNS não está no CADSUS (temporário ou incorreto) — segue incompleta." };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Fonte indisponível: sem memória (não é resposta), tenta na próxima varredura.
            logger.LogWarning(ex, "Completude de ficha sem CPF: fonte de cadastro indisponível para o CNS …{Cns4}.",
                Ultimos4(digitos));
            return manter;
        }

        // A régua é DV válido, não "11 dígitos" (adendo do ADR-0041) — a mesma da criação.
        var cpf = CpfBr.EhValido(cadsus.Cpf) ? CpfBr.SoDigitos(cadsus.Cpf) : null;
        if (cpf is null)
        {
            // Sem CPF válido a ficha do CADSUS ainda costuma trazer o NASCIMENTO — e ele sozinho
            // já torna o desafio de verificação por WhatsApp respondível (nascimento + nome).
            // Medido em 29/09/2026: 281 de 425 fichas caíram aqui no primeiro backfill.
            if (cadsus.DataNascimento is { } nascimentoSemCpf)
                await pacientes.CompletarNascimentoAsync(porCns.Id, nascimentoSemCpf, ct);
            // Filiação também: é o desempate de homônimo (decisão de 29/09/2026).
            await pacientes.CompletarFiliacaoAsync(porCns.Id, cadsus.NomeMae, cadsus.NomePai, ct);
            await MemorizarAsync(memoria, digitos, DesfechoCadsusCompletude.SemCpf, null, ct);
            return manter with { Passo = "Ficha sem CPF: o CADSUS também não tem CPF para este CNS — segue sem CPF (a recepção informa), mas o nascimento foi completado quando havia." };
        }

        try
        {
            var dono = await pacientes.ObterPorCpfAsync(cpf, ct);
            if (dono is null || dono.Id == porCns.Id)
            {
                await pacientes.DefinirCpfAsync(porCns.Id, cpf, ct);
                if (cadsus.DataNascimento is { } nascimento)
                    await pacientes.CompletarNascimentoAsync(porCns.Id, nascimento, ct);
                await pacientes.CompletarFiliacaoAsync(porCns.Id, cadsus.NomeMae, cadsus.NomePai, ct);
                await MemorizarAsync(memoria, digitos, DesfechoCadsusCompletude.CpfCarimbado, null, ct);
                return manter with { Passo = "Ficha estava sem CPF — completada pelo CADSUS (CPF carimbado; duplicata evitada na origem)." };
            }

            // O CPF já é de outro cadastro: a pessoa existe em duas metades (uma só com CNS, outra
            // só com CPF). Mesma decisão da recepção: o dono do CPF vale, o CNS vai para ele.
            await RegistrarDonoAsync(memoria, digitos, dono, porCns, ct);
            if (cadsus.DataNascimento is { } nasc2)
                await pacientes.CompletarNascimentoAsync(dono.Id, nasc2, ct);
            await pacientes.CompletarFiliacaoAsync(dono.Id, cadsus.NomeMae, cadsus.NomePai, ct);
            return new CompletudeResultado(dono.Id, dono.NomeCompleto,
                $"Ficha sem CPF era metade de um cadastro que já existia: o CADSUS confirmou o CPF de "
                + $"{dono.NomeCompleto} — a marcação usa esse cadastro e o CNS foi absorvido por ele. "
                + "A ficha antiga fica aguardando fusão.");
        }
        catch (ConflitoException ex)
        {
            // Corrida (outro fluxo carimbou outro CPF no meio) — não é falha de importação.
            logger.LogWarning("Completude de ficha sem CPF recusada para o CNS …{Cns4}: {Motivo}",
                Ultimos4(digitos), ex.Message);
            return manter;
        }
    }

    public async Task<PacienteExistenciaDto?> DestinoMemorizadoAsync(string cns, CancellationToken ct = default)
    {
        var digitos = SoDigitos(cns);
        if (digitos.Length != 15) return null;
        var memoria = await db.CadsusCompletudes.FindAsync([digitos], ct);
        if (memoria is not { Desfecho: DesfechoCadsusCompletude.RepontadoParaExistente, PacienteDestinoId: { } destinoId })
            return null;
        return await ObterExistenciaAsync(destinoId, ct);
    }

    public async Task RegistrarDonoDoCnsAsync(string cns, PacienteExistenciaDto dono, CancellationToken ct = default)
    {
        var digitos = SoDigitos(cns);
        if (digitos.Length != 15) return;
        var memoria = await db.CadsusCompletudes.FindAsync([digitos], ct);
        await RegistrarDonoAsync(memoria, digitos, dono, fichaAntiga: null, ct);
    }

    private async Task RegistrarDonoAsync(
        CadsusCompletude? memoria, string cns, PacienteExistenciaDto dono,
        PacienteExistenciaDto? fichaAntiga, CancellationToken ct)
    {
        // Idempotente: se o dono já tem CNS (este ou outro), o absorver não mexe — a memória é
        // quem garante que a resolução aponte para ele daqui em diante.
        await pacientes.AbsorverIdentificadoresAsync(dono.Id, cns, telefone: null, ct);
        await MemorizarAsync(memoria, cns, DesfechoCadsusCompletude.RepontadoParaExistente, dono.Id, ct);
        await auditoria.RegistrarAsync(
            "Paciente", dono.Id.ToString(), "AbsorveuCnsDeFichaIncompleta",
            fichaAntiga is null ? "" : $"{fichaAntiga.NomeCompleto} ({fichaAntiga.Id})",
            $"CNS …{Ultimos4(cns)} passa a resolver para este cadastro (CADSUS confirmou o CPF).", ct);
    }

    private async Task<PacienteExistenciaDto?> ObterExistenciaAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await pacientes.ObterPorIdAsync(id, ct);
            return new PacienteExistenciaDto(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Ativo);
        }
        catch (NaoEncontradoException)
        {
            return null;
        }
    }

    private async Task MemorizarAsync(
        CadsusCompletude? existente, string cns, DesfechoCadsusCompletude desfecho,
        Guid? destinoId, CancellationToken ct)
    {
        // Só STAGE (sem SaveChanges): a linha viaja na transação de quem importa. Perdê-la num
        // rollback custa no máximo uma reconsulta — os efeitos duráveis (CPF, CNS) já foram ao
        // hub FHIR por HTTP e não dependem dela.
        existente ??= await db.CadsusCompletudes.FindAsync([cns], ct);
        if (existente is null)
        {
            existente = new CadsusCompletude { Cns = cns };
            db.CadsusCompletudes.Add(existente);
        }
        existente.ConsultadoEm = DateTime.UtcNow;
        existente.Desfecho = desfecho;
        existente.PacienteDestinoId = destinoId;
    }

    private static string SoDigitos(string? v) => new([.. (v ?? string.Empty).Where(char.IsDigit)]);

    private static string Ultimos4(string v) => v.Length <= 4 ? v : v[^4..];
}
