namespace SMSMarica.Data.Entities;

/// <summary>
/// Papel profissional de <see cref="Usuario"/> (1:1). Carrega apenas campos
/// específicos de motorista. Dados pessoais base vivem em <see cref="Usuario"/>.
/// Ver ADR-0005 + ADR-0006.
/// </summary>
public class Motorista
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Cnh { get; set; } = string.Empty;

    // Auditoria (sem flag Ativo — Usuario.Ativo trata acesso; ExcluidoEm trata exclusão).
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
