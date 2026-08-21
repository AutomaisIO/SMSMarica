namespace Automais.Zap.Data.Entities;

/// <summary>
/// Trilha operacional de uma tentativa de entrega.
///
/// REGRA DURA: nada aqui identifica cidadão. Sem corpo da mensagem, sem wamid, sem
/// telefone de quem mandou. Só o número NOSSO (phone_number_id), o tipo do evento e o
/// resultado HTTP. É o que mantém o relay fora do alcance da LGPD dos municípios —
/// ele encaminha envelope, não guarda conteúdo.
/// </summary>
public sealed class EntregaLog
{
    public long Id { get; set; }

    public required string PhoneNumberId { get; set; }

    public Guid? DestinoId { get; set; }

    /// <summary>"messages", "statuses", "desconhecido"… — o campo <c>field</c> da Meta.</summary>
    public required string Tipo { get; set; }

    public bool Sucesso { get; set; }

    /// <summary>Nulo quando nem chegou a haver resposta (timeout, DNS, conexão recusada).</summary>
    public int? StatusHttp { get; set; }

    public int DuracaoMs { get; set; }

    /// <summary>Mensagem de falha, truncada. Nunca contém corpo de evento.</summary>
    public string? Erro { get; set; }

    public DateTimeOffset RecebidoEm { get; set; }
}
