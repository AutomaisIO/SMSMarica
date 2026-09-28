using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Cadastro;

public sealed record CompletarFichasRequest(IReadOnlyList<Guid> PacienteIds);

public sealed record CompletarFichaItemDto(
    Guid PacienteId,
    // CpfCarimbado, Repontado, JaTinhaCpf, SemCns, SemFichaCadsus, SemCpfCadsus,
    // NaoEncontrado, TetoOuIndisponivel ou Erro.
    string Desfecho,
    Guid? DestinoId,
    int SolicitacoesRepontadas);

public sealed record CompletarFichasResultadoDto(
    int Processados, int Ignorados, IReadOnlyList<CompletarFichaItemDto> Itens);

/// <summary>
/// Backfill das fichas que só têm CNS (estoque medido em 28/09/2026: 9.079, sendo 425 com
/// agendamento futuro — a família do caso Marcia). A importação nova já não planta essas
/// metades (<see cref="ICompletadorFichaSemCpf"/>); isto aqui limpa o passivo, em lotes
/// pequenos porque cada ficha custa uma consulta ao CADSUS.
///
/// <para>Quem enumera os candidatos é quem chama (a lista de ids vem no request): o hub é
/// API-only (ADR-0010) e varrê-lo inteiro por HTTP para filtrar "sem CPF" custaria mais que o
/// próprio backfill. As escritas, todas, saem pelo caminho oficial — completador → hub +
/// auditoria — nada de SQL por fora.</para>
/// </summary>
public interface IBackfillFichaSemCpfService
{
    Task<CompletarFichasResultadoDto> CompletarAsync(
        CompletarFichasRequest request, CancellationToken ct = default);
}

public sealed class BackfillFichaSemCpfService(
    SmsMaisDbContext db,
    ICompletadorFichaSemCpf completador,
    IPacientesService pacientes,
    IUsuarioAtualAccessor usuarioAtual,
    Auditoria.IAuditoriaService auditoria,
    ILogger<BackfillFichaSemCpfService> logger) : IBackfillFichaSemCpfService
{
    /// <summary>Teto por chamada — casa com o teto de consultas do completador por escopo, e
    /// mantém a chamada síncrona curta (cada consulta ao CADSUS leva ~1–2 s pela porta do SER).</summary>
    private const int MaxPorChamada = 20;

    /// <summary>Comunicações que ainda vão acontecer — as terminais contam a história de quem
    /// as recebeu e não se repontam.</summary>
    private static readonly StatusComunicacao[] NaoTerminais =
    [
        StatusComunicacao.Pendente,
        StatusComunicacao.AguardandoTelefoneVerificado,
        StatusComunicacao.AguardandoVerificacaoCadastral,
        StatusComunicacao.AguardandoCorrecaoContato,
    ];

    public async Task<CompletarFichasResultadoDto> CompletarAsync(
        CompletarFichasRequest request, CancellationToken ct = default)
    {
        var ids = (request.PacienteIds ?? []).Distinct().ToList();
        var processar = ids.Take(MaxPorChamada).ToList();
        var itens = new List<CompletarFichaItemDto>(processar.Count);

        foreach (var id in processar)
        {
            try
            {
                itens.Add(await CompletarUmAsync(id, ct));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Uma ficha problemática não pode matar o lote — o desfecho fica no item.
                logger.LogWarning(ex, "Backfill de ficha sem CPF falhou para o paciente {Id}.", id);
                itens.Add(new CompletarFichaItemDto(id, "Erro", null, 0));
            }
        }

        return new CompletarFichasResultadoDto(itens.Count, ids.Count - processar.Count, itens);
    }

    private async Task<CompletarFichaItemDto> CompletarUmAsync(Guid id, CancellationToken ct)
    {
        Pacientes.Dtos.PacienteDto ficha;
        try { ficha = await pacientes.ObterPorIdAsync(id, ct); }
        catch (NaoEncontradoException) { return new(id, "NaoEncontrado", null, 0); }

        if (!string.IsNullOrWhiteSpace(ficha.Cpf)) return new(id, "JaTinhaCpf", null, 0);
        var cns = new string([.. (ficha.Cns ?? string.Empty).Where(char.IsDigit)]);
        if (cns.Length != 15) return new(id, "SemCns", null, 0);

        var existencia = new PacienteExistenciaDto(ficha.Id, ficha.NomeCompleto, ficha.Cpf, ficha.Ativo);
        var resultado = await completador.CompletarAsync(existencia, cns, ct);

        if (resultado.PacienteId == ficha.Id)
        {
            // O que aconteceu está na memória que o completador acabou de preparar (ainda não
            // commitada — o SaveChanges abaixo a leva junto).
            var memoria = await db.CadsusCompletudes.FindAsync([cns], ct);
            await db.SaveChangesAsync(ct);
            return new(id, memoria?.Desfecho switch
            {
                DesfechoCadsusCompletude.CpfCarimbado => "CpfCarimbado",
                DesfechoCadsusCompletude.SemFicha => "SemFichaCadsus",
                DesfechoCadsusCompletude.SemCpf => "SemCpfCadsus",
                _ => "TetoOuIndisponivel",
            }, null, 0);
        }

        // Repontado: a pessoa já existia com CPF. As solicitações que ainda vão acontecer saem
        // da sombra para o cadastro certo (mesma decisão da recepção); as passadas ficam — são
        // história, e mexer nelas é trabalho da fusão, não do backfill.
        var agora = DateTime.UtcNow;
        var futuras = await db.Solicitacoes
            .Where(s => s.PacienteId == ficha.Id && s.ExcluidoEm == null
                && s.Status != StatusSolicitacao.Cancelada && s.DataAgendada > agora)
            .ToListAsync(ct);
        foreach (var s in futuras)
        {
            s.PacienteId = resultado.PacienteId;
            s.AtualizadoEm = agora;
            s.AtualizadoPor = usuarioAtual.UsuarioId;
        }

        // As comunicações pendentes dessas solicitações passam a falar do paciente certo — é o
        // que faz o desafio de verificação comparar com um CPF que existe (o furo da Marcia).
        var idsFuturas = futuras.Select(s => s.Id).ToList();
        var comunicacoes = await db.ComunicacoesPaciente
            .Where(c => c.SolicitacaoId != null && idsFuturas.Contains(c.SolicitacaoId.Value)
                && NaoTerminais.Contains(c.Status))
            .ToListAsync(ct);
        foreach (var c in comunicacoes) c.PacienteId = resultado.PacienteId;

        await db.SaveChangesAsync(ct);

        if (futuras.Count > 0)
        {
            await auditoria.RegistrarAsync(
                "Paciente", resultado.PacienteId.ToString(), "BackfillRepontouSolicitacoes",
                $"{ficha.NomeCompleto} ({ficha.Id})",
                $"{futuras.Count} solicitação(ões) futura(s) e {comunicacoes.Count} comunicação(ões) pendente(s)", ct);
        }

        return new(id, "Repontado", resultado.PacienteId, futuras.Count);
    }
}
