namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Etiqueta livre da ouvidoria ("recorrente", "imprensa", "MP") para agrupar manifestações fora
/// da classificação oficial. Nome único; desativar em vez de excluir para não perder histórico.
/// </summary>
public sealed class OuvidoriaMarcador
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}
