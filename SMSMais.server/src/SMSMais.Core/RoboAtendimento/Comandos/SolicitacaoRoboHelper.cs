using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.RoboAtendimento.Comandos;

internal static class SolicitacaoRoboHelper
{
    /// <summary>Agendamento futuro mais próximo do paciente (rastreado, para mutação).</summary>
    public static Task<Solicitacao?> AcharAgendamentoFuturoAsync(SmsMaisDbContext db, Guid pacienteId, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        return db.Solicitacoes
            .Where(s => s.PacienteId == pacienteId && s.ExcluidoEm == null
                && s.DataAgendada != null && s.DataAgendada >= agora)
            .OrderBy(s => s.DataAgendada)
            .FirstOrDefaultAsync(ct);
    }

    public static string Procedimento(Solicitacao s) =>
        s.EspecialidadeTexto ?? s.ProcedimentoTexto ?? "seu atendimento";
}
