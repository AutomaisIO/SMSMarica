using System.Net;
using System.Text;
using Hl7.Fhir.Model;

namespace SMSMarica.Core.Pacientes.Fhir;

public sealed class PacienteFhirClient(HttpClient http) : IPacienteFhirClient
{
    private const string MediaType = "application/fhir+json";

    public async Task<Patient> CriarAsync(Patient patient, CancellationToken ct = default)
    {
        using var resp = await http.PostAsync("fhir/Patient", Body(patient), ct);
        return await LerRecurso<Patient>(resp, ct);
    }

    public async Task<Patient?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync($"fhir/Patient/{id}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        return await LerRecurso<Patient>(resp, ct);
    }

    public async Task<Patient> AtualizarAsync(Guid id, Patient patient, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"fhir/Patient/{id}") { Content = Body(patient) };
        // Concorrência otimista: manda a versão que lemos (Meta.VersionId) no If-Match. O hub
        // devolve 409 se alguém alterou o recurso nesse meio-tempo.
        if (patient.Meta?.VersionId is { Length: > 0 } v)
            req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{v}\"");
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            throw new ConflitoVersaoHubException(id);
        return await LerRecurso<Patient>(resp, ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await http.DeleteAsync($"fhir/Patient/{id}", ct);
        if (resp.StatusCode != HttpStatusCode.NotFound)
            resp.EnsureSuccessStatusCode();
    }

    public async Task<Bundle> BuscarAsync(string? identifier = null, string? name = null, string? telecom = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(identifier)) qs.Add("identifier=" + Uri.EscapeDataString(identifier));
        if (!string.IsNullOrWhiteSpace(name)) qs.Add("name=" + Uri.EscapeDataString(name));
        if (!string.IsNullOrWhiteSpace(telecom)) qs.Add("telecom=" + Uri.EscapeDataString(telecom));
        var url = "fhir/Patient" + (qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty);

        using var resp = await http.GetAsync(url, ct);
        return await LerRecurso<Bundle>(resp, ct);
    }

    public async Task<Bundle> BuscarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var url = "fhir/Patient?_id=" + string.Join(',', ids);
        using var resp = await http.GetAsync(url, ct);
        return await LerRecurso<Bundle>(resp, ct);
    }

    public async Task<Bundle> ListarParaManutencaoAsync(Guid? cursor, int count, CancellationToken ct = default)
    {
        var url = $"fhir/Patient/_manutencao?_count={count}" + (cursor is Guid c ? $"&_cursor={c}" : string.Empty);
        using var resp = await http.GetAsync(url, ct);
        return await LerRecurso<Bundle>(resp, ct);
    }

    private static StringContent Body(Resource r) =>
        new(FhirJson.Serialize(r), Encoding.UTF8, MediaType);

    private static async Task<T> LerRecurso<T>(HttpResponseMessage resp, CancellationToken ct) where T : Resource
    {
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Hub FHIR retornou {(int)resp.StatusCode}: {json}");
        return FhirJson.Parse<T>(json);
    }
}
