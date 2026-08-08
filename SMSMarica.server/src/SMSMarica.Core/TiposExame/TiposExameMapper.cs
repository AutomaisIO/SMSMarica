using SMSMarica.Core.TiposExame.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.TiposExame;

internal static class TiposExameMapper
{
    public static TipoExameDto ParaDto(TipoExame t) => new(
        t.Id,
        t.Nome,
        t.ProcedimentoSigtapId,
        t.ProcedimentoSigtap?.Codigo ?? string.Empty,
        t.ProcedimentoSigtap?.Nome ?? string.Empty,
        t.ModalidadeDicom,
        t.RequestedProcedureDescription,
        t.ScheduledProcedureStepDescription,
        t.CodigosProtocolo,
        t.TempoEstimadoMinutos,
        t.UnidadePadraoId,
        t.UnidadePadrao?.Nome,
        t.Ativo,
        t.EnviarParaWorklist,
        t.CriadoEm);

    public static TipoExameListItemDto ParaListItem(TipoExame t) => new(
        t.Id,
        t.Nome,
        t.ModalidadeDicom,
        t.ProcedimentoSigtap?.Codigo ?? string.Empty,
        t.TempoEstimadoMinutos,
        t.Ativo,
        t.EnviarParaWorklist);
}
