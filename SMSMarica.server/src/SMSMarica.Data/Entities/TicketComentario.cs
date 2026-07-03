namespace SMSMarica.Data.Entities;

/// <summary>
/// Mensagem na conversa de um ticket. Tanto o autor do ticket quanto a equipe podem comentar.
/// <see cref="Interno"/> marca uma nota visível apenas para a gestão (não para o autor).
/// </summary>
public sealed class TicketComentario
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }

    /// <summary>Usuário que escreveu o comentário.</summary>
    public Guid? AutorId { get; set; }

    public string Texto { get; set; } = string.Empty;

    /// <summary>Nota interna da equipe (não exibida ao autor do ticket).</summary>
    public bool Interno { get; set; }

    public DateTime CriadoEm { get; set; }

    public Ticket? Ticket { get; set; }
    public ICollection<TicketAnexo> Anexos { get; set; } = [];
}
