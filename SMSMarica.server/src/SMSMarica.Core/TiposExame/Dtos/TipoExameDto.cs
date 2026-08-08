using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame.Dtos;

public sealed record TipoExameDto(
    Guid Id,
    string Nome,
    Guid ProcedimentoSigtapId,
    string ProcedimentoSigtapCodigo,
    string ProcedimentoSigtapNome,
    ModalidadeDicom ModalidadeDicom,
    string RequestedProcedureDescription,
    string ScheduledProcedureStepDescription,
    IReadOnlyList<string> CodigosProtocolo,
    int? TempoEstimadoMinutos,
    Guid? UnidadePadraoId,
    string? UnidadePadraoNome,
    bool Ativo,
    bool EnviarParaWorklist,
    DateTime CriadoEm);

public sealed record TipoExameListItemDto(
    Guid Id,
    string Nome,
    ModalidadeDicom ModalidadeDicom,
    string ProcedimentoSigtapCodigo,
    int? TempoEstimadoMinutos,
    bool Ativo,
    bool EnviarParaWorklist);
