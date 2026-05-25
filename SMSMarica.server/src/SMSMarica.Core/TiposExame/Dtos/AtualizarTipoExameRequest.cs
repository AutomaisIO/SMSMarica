using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame.Dtos;

public sealed record AtualizarTipoExameRequest(
    string Nome,
    Guid ProcedimentoSigtapId,
    ModalidadeDicom ModalidadeDicom,
    string RequestedProcedureDescription,
    string ScheduledProcedureStepDescription,
    IReadOnlyList<string>? CodigosProtocolo,
    int? TempoEstimadoMinutos,
    Guid? UnidadePadraoId,
    bool Ativo);
