using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame.Dtos;

public sealed record CadastrarTipoExameRequest(
    string Nome,
    Guid ProcedimentoSigtapId,
    ModalidadeDicom ModalidadeDicom,
    string RequestedProcedureDescription,
    string ScheduledProcedureStepDescription,
    IReadOnlyList<string>? CodigosProtocolo,
    int? TempoEstimadoMinutos,
    Guid? UnidadePadraoId,
    bool EnviarParaWorklist = true);
