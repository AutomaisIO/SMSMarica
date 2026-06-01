namespace Automais.Fhir.Core.Fhir;

/// <summary>Utilitários para referências FHIR (<c>Reference.reference</c>).</summary>
public static class FhirRef
{
    /// <summary>Extrai o id de uma referência <c>"Patient/{guid}"</c> (aceita guid puro). Null se não casar.</summary>
    public static Guid? ParseId(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var ultimo = reference.Split('/')[^1];
        return Guid.TryParse(ultimo, out var g) ? g : null;
    }
}
