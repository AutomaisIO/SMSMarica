using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

public sealed record CadastrarSolicitacaoExameRequest(
    Guid PacienteId,
    Guid TipoExameId,
    Guid UnidadeId,

    // Solicitante: SolicitanteUsuarioId é opcional (null = externo).
    // Nome/registro/UF são sempre obrigatórios (snapshot). Conselho = "CRM" (médico)
    // ou "COREN" (enfermeiro); null/omitido → o serviço assume "CRM" (retrocompat).
    Guid? SolicitanteUsuarioId,
    string SolicitanteNome,
    string SolicitanteNumConselho,
    string SolicitanteUfConselho,
    string? SolicitanteConselho,

    string? CodigoSolicitacao,
    string? ChaveConfirmacao,
    string? Justificativa,

    PrioridadeSolicitacao Prioridade,
    string? Observacoes,
    DateTime? DataAgendada);
