using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace SMSMais.Core.Pacientes.Fhir;

/// <summary>Serialização/parse de recursos FHIR (Firely) para falar com o hub.</summary>
internal static class FhirJson
{
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    public static string Serialize(Resource resource) => JsonSerializer.Serialize(resource, Options);

    public static T Parse<T>(string json) where T : Resource =>
        JsonSerializer.Deserialize<T>(json, Options)!;
}
