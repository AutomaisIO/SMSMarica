namespace SMSMarica.Data.Entities;

public class Paciente
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Cns { get; set; }
    public Gps GpsResidencia { get; set; } = new(0, 0);
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
