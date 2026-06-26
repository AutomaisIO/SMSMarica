using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

public sealed record SolicitacaoExameDto(
    Guid Id,
    string AccessionNumber,
    string StudyInstanceUID,
    string? WorklistItemUid,

    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,

    Guid TipoExameId,
    string TipoExameNome,
    ModalidadeDicom ModalidadeDicom,

    Guid UnidadeId,
    string UnidadeNome,

    Guid? SolicitanteUsuarioId,
    string SolicitanteNome,
    string SolicitanteCrm,
    string SolicitanteUfCrm,

    string? CodigoSolicitacao,
    string? ChaveConfirmacao,
    string? Justificativa,

    StatusSolicitacaoExame Status,
    PrioridadeSolicitacao Prioridade,
    string? Observacoes,

    DateTime? DataAgendada,
    DateTime? IniciadoEm,
    DateTime? RealizadoEm,
    string? ErroIntegracaoPacs,

    DateTime? CanceladoEm,
    string? MotivoCancelamento,

    int TentativasEnvio,
    DateTime? UltimaTentativaEm,
    DateTime? ProximaTentativaEm,

    DateTime CriadoEm,
    DateTime? AtualizadoEm);

public sealed record SolicitacaoExameListItemDto(
    Guid Id,
    string AccessionNumber,
    Guid PacienteId,
    string PacienteNome,
    Guid TipoExameId,
    string TipoExameNome,
    ModalidadeDicom ModalidadeDicom,
    string SolicitanteNome,
    StatusSolicitacaoExame Status,
    PrioridadeSolicitacao Prioridade,
    DateTime? DataAgendada,
    DateTime CriadoEm);
