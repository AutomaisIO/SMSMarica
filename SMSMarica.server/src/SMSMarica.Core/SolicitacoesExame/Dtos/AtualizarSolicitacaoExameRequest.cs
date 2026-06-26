using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

/// <summary>Atualiza dados editáveis de uma solicitação ainda em status Solicitada.</summary>
public sealed record AtualizarSolicitacaoExameRequest(
    Guid TipoExameId,
    Guid UnidadeId,

    Guid? SolicitanteUsuarioId,
    string SolicitanteNome,
    string SolicitanteCrm,
    string SolicitanteUfCrm,

    string? CodigoSolicitacao,
    string? ChaveConfirmacao,
    string? Justificativa,
    PrioridadeSolicitacao Prioridade,
    string? Observacoes,
    DateTime? DataAgendada);
