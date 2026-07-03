namespace SMSMarica.Data.Entities;

/// <summary>
/// Anexo (print de tela/imagem) de um ticket ou de um comentário. O binário vive na
/// tabela genérica de mídias (bytea, dedup por hash) — aqui guardamos só a referência.
/// </summary>
public sealed class TicketAnexo
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }

    /// <summary>Quando o anexo pertence a um comentário específico (senão é anexo do ticket em si).</summary>
    public Guid? ComentarioId { get; set; }

    /// <summary>FK para <c>smsmarica.midia</c> (binário compartilhado).</summary>
    public Guid MidiaId { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public Ticket? Ticket { get; set; }
    public TicketComentario? Comentario { get; set; }
}
