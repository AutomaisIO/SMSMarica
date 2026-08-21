namespace Automais.Zap.Data.Entities;

/// <summary>
/// Um WhatsApp Business Account conhecido pelo console.
///
/// Existe porque a Graph API não deixa listar os WABAs de um business sem a permissão
/// <c>business_management</c>, que o token do System User não tem. Então o console guarda
/// os que o operador informou e consulta cada um pelo id.
/// </summary>
public sealed class Waba
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Id do WABA na Meta.</summary>
    public required string WabaId { get; set; }

    /// <summary>Nome como a Meta devolve. Preenchido na consulta, não digitado.</summary>
    public string? Nome { get; set; }

    /// <summary>Destino sugerido ao importar os números deste WABA. Opcional.</summary>
    public Guid? DestinoId { get; set; }
    public Destino? Destino { get; set; }

    public string? Observacao { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? SincronizadoEm { get; set; }
}
