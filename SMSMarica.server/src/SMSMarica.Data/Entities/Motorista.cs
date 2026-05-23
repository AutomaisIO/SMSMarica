namespace SMSMarica.Data.Entities;

/// <summary>
/// Papel profissional de <see cref="Usuario"/> (1:1). Carrega apenas campos
/// específicos de motorista. Dados pessoais base (nome, CPF, endereço, foto)
/// vivem em <see cref="Usuario"/>. Ver ADR-0005.
/// </summary>
public class Motorista
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Cnh { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
