namespace SMSMarica.Data.Entities.Fhir.Lookups;

/// <summary>
/// País (ISO 3166-1). Lookup populada por seed. Default no contexto: BRA.
/// </summary>
public sealed class PaisIso
{
    /// <summary>Código ISO 3166-1 alfa-3 (ex.: BRA, USA, ARG).</summary>
    public string CodigoAlfa3 { get; set; } = string.Empty;

    /// <summary>Código ISO 3166-1 alfa-2 (ex.: BR, US, AR).</summary>
    public string CodigoAlfa2 { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;
}
