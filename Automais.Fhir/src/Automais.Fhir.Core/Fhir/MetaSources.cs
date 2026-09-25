namespace Automais.Fhir.Core.Fhir;

/// <summary>
/// URIs de origem (<c>Meta.source</c>) por sistema-fonte. Obrigatório em todo
/// recurso fhir.* (ADR-0009). Centralizado para não duplicar string.
/// </summary>
public static class MetaSources
{
    private const string Base = "https://smsmarica.saude.marica/source/";

    /// <summary>Recurso nascido no próprio hub (não replicado de um PEP).</summary>
    public const string Hub = Base + "smsmarica";

    public const string Salux = Base + "salux";

    /// <summary>Prime Saúde (Eco Sistemas) — atenção especializada de Maricá.</summary>
    public const string Prime = Base + "prime";
    public const string Esus = Base + "esus";
    public const string Pacs = Base + "pacs";
}
