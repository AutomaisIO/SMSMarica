using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame.Dtos;

public sealed record CadastrarTipoExameRequest(
    string Nome,
    /// <summary>Correlação de faturamento. Opcional — não identifica nem libera a execução.</summary>
    Guid? ProcedimentoSigtapId,
    /// <summary>O <c>pa</c> do SISREG, quando conhecido.</summary>
    string? CodigoSisreg,
    ModalidadeDicom ModalidadeDicom,
    string RequestedProcedureDescription,
    string ScheduledProcedureStepDescription,
    IReadOnlyList<string>? CodigosProtocolo,
    int? TempoEstimadoMinutos,
    Guid? UnidadePadraoId,
    bool EnviarParaWorklist = true);
