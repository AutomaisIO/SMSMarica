namespace SMSMarica.Data.Entities;

public class Avaliacao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public int Nota { get; set; }
    public string? Comentario { get; set; }
    public DateTime CriadoEm { get; set; }

    public SessaoDeTratamento? Sessao { get; set; }
}
