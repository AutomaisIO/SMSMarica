using System.Net;
using System.Text;
using Hl7.Fhir.Model;
using SMSMarica.Core.Pacientes.Fhir;

namespace SMSMarica.Core.Integracoes.Pep.Fhir;

/// <summary>
/// Implementação HTTP do <see cref="IHubFhirEscritor"/>. Fala com o hub na mesma base URL
/// (<c>Fhir:BaseUrl</c>) e content-type (<c>application/fhir+json</c>) dos demais clients.
/// </summary>
public sealed class HubFhirEscritor(HttpClient http) : IHubFhirEscritor
{
    private const string MediaType = "application/fhir+json";

    public async Task<Resource> CriarAsync(Resource recurso, CancellationToken ct = default)
    {
        using var resp = await http.PostAsync($"fhir/{recurso.TypeName}", Body(recurso), ct);
        return await Ler(resp, ct);
    }

    public async Task<Resource> AtualizarAsync(string tipo, string id, Resource recurso, CancellationToken ct = default)
    {
        using var resp = await http.PutAsync($"fhir/{tipo}/{id}", Body(recurso), ct);
        return await Ler(resp, ct);
    }

    public async Task ExcluirAsync(string tipo, string id, CancellationToken ct = default)
    {
        using var resp = await http.DeleteAsync($"fhir/{tipo}/{id}", ct);
        if (resp.StatusCode != HttpStatusCode.NotFound)
            resp.EnsureSuccessStatusCode();
    }

    public Task<Bundle> BuscarPorPacienteAsync(string tipo, string pacienteId, CancellationToken ct = default) =>
        BuscarBundle($"fhir/{tipo}?patient={Uri.EscapeDataString(pacienteId)}", ct);

    public Task<Bundle> BuscarPorIdentifierAsync(string tipo, string system, string value, CancellationToken ct = default) =>
        BuscarBundle($"fhir/{tipo}?identifier={Uri.EscapeDataString($"{system}|{value}")}", ct);

    public Task<Bundle> ListarAsync(string tipo, CancellationToken ct = default) =>
        BuscarBundle($"fhir/{tipo}", ct);

    private async Task<Bundle> BuscarBundle(string url, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Hub FHIR retornou {(int)resp.StatusCode} em GET {url}: {json}");
        return FhirJson.Parse<Bundle>(json);
    }

    private static StringContent Body(Resource r) => new(FhirJson.Serialize(r), Encoding.UTF8, MediaType);

    private static async Task<Resource> Ler(HttpResponseMessage resp, CancellationToken ct)
    {
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Hub FHIR retornou {(int)resp.StatusCode}: {json}");
        return FhirJson.Parse<Resource>(json);
    }
}
