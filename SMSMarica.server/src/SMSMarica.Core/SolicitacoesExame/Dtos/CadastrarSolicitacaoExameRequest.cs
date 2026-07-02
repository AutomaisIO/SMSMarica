using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

public sealed record CadastrarSolicitacaoExameRequest(
    Guid PacienteId,
    Guid TipoExameId,
    Guid UnidadeId,

    // Unidade que solicitou o exame (opcional — não quebra fluxos existentes).
    Guid? UnidadeSolicitanteId,

    // Solicitante = só o nome (texto livre, obrigatório). Não cadastramos médico/enfermeiro
    // nem CRM/COREN pelo front (colunas do banco mantidas mas não preenchidas por aqui).
    string SolicitanteNome,

    string? CodigoSolicitacao,
    string? ChaveConfirmacao,
    string? Justificativa,

    PrioridadeSolicitacao Prioridade,
    string? Observacoes,
    DateTime? DataAgendada);
