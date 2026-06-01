using System.Net;
using System.Text;
using Hl7.Fhir.Model;
using SMSMarica.Core.Pacientes.Fhir;

namespace SMSMarica.Core.Medicos.Fhir;

public sealed class PractitionerFhirClient(HttpClient http) : IPractitionerFhirClient
{
    private const string MediaType = "application/fhir+json";

    public async Task<Practitioner> CriarAsync(Practitioner p, CancellationToken ct = default)
    {
        using var resp = await http.PostAsync("fhir/Practitioner", Body(p), ct);
        return await Ler<Practitioner>(resp, ct);
    }

    public async Task<Practitioner?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync($"fhir/Practitioner/{id}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        return await Ler<Practitioner>(resp, ct);
    }

    public async Task<Practitioner> AtualizarAsync(Guid id, Practitioner p, CancellationToken ct = default)
    {
        using var resp = await http.PutAsync($"fhir/Practitioner/{id}", Body(p), ct);
        return await Ler<Practitioner>(resp, ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await http.DeleteAsync($"fhir/Practitioner/{id}", ct);
        if (resp.StatusCode != HttpStatusCode.NotFound) resp.EnsureSuccessStatusCode();
    }

    public async Task<Bundle> BuscarAsync(
        string? identifier = null,
        string? name = null,
        string? conselho = null,
        string? conselhoNe = null,
        CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(identifier)) qs.Add("identifier=" + Uri.EscapeDataString(identifier));
        if (!string.IsNullOrWhiteSpace(name)) qs.Add("name=" + Uri.EscapeDataString(name));
        if (!string.IsNullOrWhiteSpace(conselho)) qs.Add("conselho=" + Uri.EscapeDataString(conselho));
        if (!string.IsNullOrWhiteSpace(conselhoNe)) qs.Add("conselhoNe=" + Uri.EscapeDataString(conselhoNe));
        var url = "fhir/Practitioner" + (qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty);
        using var resp = await http.GetAsync(url, ct);
        return await Ler<Bundle>(resp, ct);
    }

    private static StringContent Body(Resource r) => new(FhirJson.Serialize(r), Encoding.UTF8, MediaType);

    private static async Task<T> Ler<T>(HttpResponseMessage resp, CancellationToken ct) where T : Resource
    {
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Hub FHIR retornou {(int)resp.StatusCode}: {json}");
        return FhirJson.Parse<T>(json);
    }
}
