using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

public sealed record CadastrarSolicitacaoExameRequest(
    Guid PacienteId,
    Guid TipoExameId,
    Guid UnidadeId,

    // Solicitante: SolicitanteUsuarioId é opcional (null = médico externo).
    // Nome/CRM/UF são sempre obrigatórios (snapshot).
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
