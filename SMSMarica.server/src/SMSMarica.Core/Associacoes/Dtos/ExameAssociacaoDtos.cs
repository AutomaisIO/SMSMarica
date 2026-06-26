using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes.Dtos;

/// <summary>Pedido de associação manual de um estudo do PACS a uma solicitação.</summary>
public sealed record AssociarExameRequest(
    string StudyInstanceUID,
    string AccessionNumber,
    string? AccessionNumberDicomOriginal = null);

/// <summary>
/// Vínculo resolvido de um estudo (explícito via tabela, ou implícito por
/// StudyInstanceUID de worklist). Usado na listagem e no preview.
/// </summary>
public sealed record ExameAssociacaoDto(
    string StudyInstanceUID,
    Guid SolicitacaoExameId,
    string AccessionNumber,
    Guid PacienteId,
    string? PacienteNome,
    bool Explicita,
    OrigemAssociacaoExame? Origem,
    PrioridadeSolicitacao Prioridade);

/// <summary>Vínculo mínimo (solicitação + paciente) usado pelo gate de laudar.</summary>
public sealed record VinculoExame(Guid SolicitacaoExameId, Guid PacienteId);
