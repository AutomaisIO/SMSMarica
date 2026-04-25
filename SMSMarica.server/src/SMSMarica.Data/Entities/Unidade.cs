namespace SMSMarica.Data.Entities;

public class Unidade
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Endereco? Endereco { get; set; }
    public string? Telefone { get; set; }
    public Gps? Gps { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
