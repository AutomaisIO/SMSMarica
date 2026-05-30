using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;
using Automais.Fhir.Core.Fhir;

namespace Automais.Fhir.Api.Infra;

/// <summary>Helpers para devolver recursos FHIR como <c>application/fhir+json</c>.</summary>
public static class FhirResponse
{
    public const string MediaType = "application/fhir+json";

    public static ContentResult Recurso(Resource resource, int status = StatusCodes.Status200OK) => new()
    {
        Content = FhirJson.Serialize(resource),
        ContentType = MediaType,
        StatusCode = status,
    };
}
