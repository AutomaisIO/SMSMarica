namespace Automais.Zap.Data.Entities;

/// <summary>
/// Um cliente da plataforma — na prática, um município. É o eixo: usuários, WABAs, números e
/// trilha de entrega penduram aqui.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nome { get; set; }

    /// <summary>
    /// Desligar corta o recebimento de TODOS os WABAs deste tenant. É a alavanca comercial:
    /// funciona porque o canal é nosso, sem phone-home e sem cooperação da instância.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public DateTimeOffset? SuspensoEm { get; set; }
    public string? SuspensoMotivo { get; set; }

    public string? Observacao { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? AtualizadoEm { get; set; }

    public ICollection<Waba> Wabas { get; set; } = [];
    public ICollection<UsuarioTenant> Usuarios { get; set; } = [];
}
