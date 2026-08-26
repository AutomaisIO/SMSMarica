namespace SMSMais.Core.RoboAtendimento.Dtos;

/// <summary>Um atendente marca uma resposta do robô como errada (para treinamento).</summary>
public sealed record RegistrarRoboErroRequest(
    Guid ConversaId,
    Guid? MensagemWhatsAppId,
    string? Nota);

/// <summary>Revisão de um erro marcado (quem cuida do treinamento).</summary>
public sealed record RevisarRoboErroRequest(
    string Status,   // "Revisado" | "Descartado"
    string? Nota);

/// <summary>Erro do robô na lista de revisão de treinamento.</summary>
public sealed record RoboErroDto(
    Guid Id,
    Guid ConversaId,
    Guid? MensagemWhatsAppId,
    string? Assunto,
    string? Trecho,
    string? Nota,
    string Status,
    DateTime CriadoEm,
    string? CriadoPorNome,
    DateTime? RevisadoEm,
    string? RevisaoNota);
