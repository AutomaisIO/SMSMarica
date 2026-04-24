namespace SMSMarica.Data.Entities;

/// <summary>Dados do contato de emergência do paciente.</summary>
public sealed class ContatoEmergencia
{
    public string Nome { get; set; } = string.Empty;
    public string? Parentesco { get; set; }
    public string Telefone { get; set; } = string.Empty;
}
