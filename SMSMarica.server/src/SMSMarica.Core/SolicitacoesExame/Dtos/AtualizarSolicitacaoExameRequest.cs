using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

/// <summary>Atualiza dados editáveis de uma solicitação ainda em status Solicitada.</summary>
public sealed record AtualizarSolicitacaoExameRequest(
    Guid TipoExameId,
    Guid UnidadeId,
    Guid? UnidadeSolicitanteId,

    // Solicitante = só o nome (texto livre, obrigatório).
    string SolicitanteNome,

    string? CodigoSolicitacao,
    string? ChaveConfirmacao,
    string? Justificativa,
    PrioridadeSolicitacao Prioridade,
    string? Observacoes,
    DateTime? DataAgendada,

    // Data em que o exame foi solicitado (dia de calendário; opcional).
    DateOnly? DataSolicitacao = null);
