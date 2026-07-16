using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

/// <summary>Resultado do backfill de <c>data_estudo</c> (data DICOM dos exames antigos).</summary>
public sealed record BackfillDataEstudoResultado(
    int Candidatos,
    int Atualizados,
    int SemDataDicom,
    int Erros,
    bool DryRun);

/// <summary>
/// Preenche <c>data_estudo</c> (StudyDate/StudyTime do DICOM) nos exames que ficaram
/// com o campo nulo — realizados antes de a coluna existir. Reusa o MESMO leitor
/// QIDO-RS dos exames novos (<see cref="IConsultaStudyClient.ObterDataHoraEstudoAsync"/>),
/// então a data fica idêntica. Idempotente: só toca linhas com <c>data_estudo</c> nulo
/// e grava via <c>ExecuteUpdate</c> (não altera <c>atualizado_em</c> nem auditoria).
/// </summary>
public interface IBackfillDataEstudoService
{
    Task<BackfillDataEstudoResultado> ExecutarAsync(int limite, bool dryRun, CancellationToken cancellationToken = default);
}

public sealed class BackfillDataEstudoService(
    SmsMaricaDbContext db,
    IConsultaStudyClient consultaStudy,
    ILogger<BackfillDataEstudoService> logger) : IBackfillDataEstudoService
{
    public async Task<BackfillDataEstudoResultado> ExecutarAsync(
        int limite, bool dryRun, CancellationToken cancellationToken = default)
    {
        var lim = limite is <= 0 or > 5000 ? 1000 : limite;

        // Candidatos: realizados/laudados, com study no PACS e sem data_estudo ainda.
        var candidatos = await db.ExamesImagem.AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                && s.DataEstudo == null
                && (s.Status == StatusSolicitacaoExame.Realizada || s.Status == StatusSolicitacaoExame.Laudada)
                && s.StudyInstanceUID != "")
            .OrderByDescending(s => s.CriadoEm)
            .Take(lim)
            .Select(s => new { s.Id, s.StudyInstanceUID })
            .ToListAsync(cancellationToken);

        if (dryRun)
            return new BackfillDataEstudoResultado(candidatos.Count, 0, 0, 0, DryRun: true);

        int atualizados = 0, semData = 0, erros = 0;
        foreach (var c in candidatos)
        {
            try
            {
                var data = await consultaStudy.ObterDataHoraEstudoAsync(c.StudyInstanceUID, cancellationToken);
                if (data is null)
                {
                    // Estudo não está mais no PACS ou sem as tags — mantém NULL (fallback RealizadoEm).
                    semData++;
                    continue;
                }

                await db.ExamesImagem
                    .Where(s => s.Id == c.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.DataEstudo, data), cancellationToken);
                atualizados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                erros++;
                logger.LogWarning(ex, "Backfill data_estudo falhou para solicitacao {Id}.", c.Id);
            }
        }

        logger.LogInformation(
            "Backfill data_estudo: {Candidatos} candidatos, {Atualizados} atualizados, {SemData} sem data DICOM, {Erros} erros.",
            candidatos.Count, atualizados, semData, erros);

        return new BackfillDataEstudoResultado(candidatos.Count, atualizados, semData, erros, DryRun: false);
    }
}
