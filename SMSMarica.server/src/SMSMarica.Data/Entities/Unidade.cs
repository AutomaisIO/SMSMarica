namespace SMSMarica.Data.Entities;

public class Unidade
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public Gps Gps { get; set; } = new(0, 0);
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
