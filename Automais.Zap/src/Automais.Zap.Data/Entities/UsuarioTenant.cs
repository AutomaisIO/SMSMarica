namespace Automais.Zap.Data.Entities;

/// <summary>
/// Vínculo N:N entre operador e tenant. Só vale para quem NÃO é global — usuário global
/// enxerga todos os tenants sem precisar de linha aqui.
/// </summary>
public sealed class UsuarioTenant
{
    public Guid UsuarioId { get; set; }
    public UsuarioAdmin? Usuario { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}
