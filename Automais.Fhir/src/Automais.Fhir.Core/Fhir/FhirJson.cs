using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace Automais.Fhir.Core.Fhir;

/// <summary>
/// Serialização/parse de recursos FHIR via Firely SDK (System.Text.Json).
/// É a fronteira entre o objeto Firely (em memória) e o JSON guardado em jsonb
/// ou trafegado na API. Centraliza as <see cref="JsonSerializerOptions"/> FHIR.
/// </summary>
public static class FhirJson
{
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    /// <summary>Serializa um recurso FHIR para JSON (formato application/fhir+json).</summary>
    public static string Serialize(Resource resource) =>
        JsonSerializer.Serialize(resource, Options);

    /// <summary>
    /// Faz parse de JSON para um recurso FHIR tipado. Lança
    /// <see cref="DeserializationFailedException"/> se o JSON for inválido/não-conforme.
    /// </summary>
    public static T Parse<T>(string json) where T : Resource =>
        JsonSerializer.Deserialize<T>(json, Options)!;
}
