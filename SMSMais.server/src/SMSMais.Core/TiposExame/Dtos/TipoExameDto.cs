using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.TiposExame.Dtos;

public sealed record TipoExameDto(
    Guid Id,
    /// <summary>Nome do procedimento no SISREG, em MAIÚSCULAS. É o que aparece em toda a UI.</summary>
    string Nome,
    /// <summary>O <c>pa</c> do SISREG. Null quando o SISREG não informou (acontece em ~1/3 das linhas).</summary>
    string? CodigoSisreg,
    /// <summary>Criado pela importação e ainda sem configuração DICOM feita por gente.</summary>
    bool AutoCriado,
    Guid? ProcedimentoSigtapId,
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
    string? CodigoSisreg,
    bool AutoCriado,
    /// <summary>
    /// Nasceu da importação e ainda não foi configurado para o PACS. É a pendência que substituiu
    /// o antigo "exame sem tipo mapeado".
    /// </summary>
    bool AguardandoConfiguracaoDicom,
    ModalidadeDicom ModalidadeDicom,
    string ProcedimentoSigtapCodigo,
    int? TempoEstimadoMinutos,
    bool Ativo,
    bool EnviarParaWorklist);
