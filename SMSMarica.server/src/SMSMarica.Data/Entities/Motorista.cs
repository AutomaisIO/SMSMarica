namespace SMSMarica.Data.Entities;

public class Motorista
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Cnh { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public Endereco? Endereco { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
