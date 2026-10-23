namespace SMSMais.Data.Entities.ComunicacaoInterna;

/// <summary>
/// Até onde cada pessoa leu uma conversa (não-lidas = mensagens de OUTROS depois de
/// <see cref="LidaAte"/>) e se ela silenciou a conversa. Criada sob demanda: sem linha = nada lido,
/// não silenciada.
/// </summary>
public sealed class ConversaInternaLeitura
{
    public Guid ConversaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime? LidaAte { get; set; }
    public bool Silenciada { get; set; }

    public ConversaInterna? Conversa { get; set; }
}
