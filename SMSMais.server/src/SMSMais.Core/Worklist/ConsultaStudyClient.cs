using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Worklist;

/// <summary>
/// Implementação leve do QIDO-RS. HttpClient configurado com a base do AE
/// <c>PACS-CDT</c> (não o WORK-CDT).
/// </summary>
public sealed class ConsultaStudyClient(HttpClient http, ILogger<ConsultaStudyClient> logger) : IConsultaStudyClient
{
    private readonly HttpClient _http = http;
    private readonly ILogger<ConsultaStudyClient> _logger = logger;

    public async Task<bool> StudyExistePorStudyUidAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return false;
        var arr = await ConsultarAsync(
            $"studies?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUID)}&limit=1", cancellationToken);
        return arr is { ValueKind: JsonValueKind.Array } a && a.GetArrayLength() > 0;
    }

    // Página do QIDO por data — abaixo do cap server-side de resultados do dcm4chee
    // (QidoMaxNumberOfResults costuma ser 100): paginar com offset evita truncar a janela.
    private const int TamanhoPaginaStudies = 100;

    public async Task<IReadOnlyList<EstudoPacsRecente>> BuscarStudiesPorDataAsync(
        DateOnly inicio, DateOnly fim, int limite, CancellationToken cancellationToken = default)
    {
        static string F(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var intervalo = inicio == fim ? F(inicio) : $"{F(inicio)}-{F(fim)}";
        var teto = Math.Max(1, limite);

        var lista = new List<EstudoPacsRecente>();
        var offset = 0;
        while (lista.Count < teto)
        {
            // Página de tamanho FIXO (offset avança pelo nº de linhas RECEBIDAS, nunca por
            // contagem filtrada) e ordenação determinística — sem orderby o dcm4chee não
            // garante ordem entre requisições e o paging por offset pula/duplica linhas.
            // Falha do PACS aqui LANÇA (ConsultarPaginaAsync): lista parcial silenciosa
            // seria indistinguível de "janela vazia" para o chamador.
            var arr = await ConsultarPaginaAsync(
                $"studies?StudyDate={intervalo}&includefield=00080050&includefield=0020000D&includefield=00100020" +
                $"&orderby=-StudyDate,-StudyTime&limit={TamanhoPaginaStudies}&offset={offset}",
                cancellationToken);
            if (arr is not { ValueKind: JsonValueKind.Array } a || a.GetArrayLength() == 0) break;

            foreach (var estudo in a.EnumerateArray())
            {
                var uid = Tag(estudo, "0020000D");
                if (string.IsNullOrWhiteSpace(uid)) continue;
                lista.Add(new EstudoPacsRecente(uid!, Tag(estudo, "00080050"), Tag(estudo, "00100020")));
            }

            if (a.GetArrayLength() < TamanhoPaginaStudies) break; // página curta = acabou
            offset += a.GetArrayLength();
        }
        return lista.Count > teto ? lista[..teto] : lista;
    }

    public async Task<DateTime?> ObterDataHoraEstudoAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return null;
        var arr = await ConsultarAsync(
            $"studies?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUID)}&includefield=00080020&includefield=00080030&limit=1",
            cancellationToken);
        if (arr is not { ValueKind: JsonValueKind.Array } a || a.GetArrayLength() == 0) return null;

        var estudo = a[0];
        return CombinarDataHoraDicom(Tag(estudo, "00080020"), Tag(estudo, "00080030"));
    }

    /// <summary>Tag privada dcm4chee (7777,1037) — SendingApplicationEntityTitleOfSeries.</summary>
    private const string TagAeOrigem = "77771037";

    public async Task<string?> ObterAeOrigemAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return null;
        // Nível de SÉRIE: no de estudo a tag filtra mas volta vazia. limit=1 basta — as séries
        // de um mesmo estudo saem do mesmo equipamento.
        var arr = await ConsultarAsync(
            $"series?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUID)}&includefield={TagAeOrigem}&limit=1",
            cancellationToken);
        if (arr is not { ValueKind: JsonValueKind.Array } a || a.GetArrayLength() == 0) return null;

        var ae = Tag(a[0], TagAeOrigem);
        return string.IsNullOrWhiteSpace(ae) ? null : ae.Trim();
    }

    public async Task<string?> ObterNomePacienteAsync(string studyInstanceUID, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return null;
        var arr = await ConsultarAsync(
            $"studies?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUID)}&includefield=00100010&limit=1",
            cancellationToken);
        if (arr is not { ValueKind: JsonValueKind.Array } a || a.GetArrayLength() == 0) return null;

        return LimparNomePn(NomePaciente(a[0]));
    }

    /// <summary>
    /// Lê PatientName (0010,0010) do DICOM-JSON. VR PN traz o valor como objeto
    /// <c>{ "Alphabetic": "Familia^Nome^..." }</c> (grupos de caractere), mas alguns
    /// equipamentos emitem uma string simples — cobre as duas formas.
    /// </summary>
    private static string? NomePaciente(JsonElement estudo)
    {
        if (estudo.ValueKind != JsonValueKind.Object) return null;
        if (!estudo.TryGetProperty("00100010", out var campo)) return null;
        if (!campo.TryGetProperty("Value", out var valor) || valor.ValueKind != JsonValueKind.Array || valor.GetArrayLength() == 0)
            return null;
        var primeiro = valor[0];
        if (primeiro.ValueKind == JsonValueKind.String) return primeiro.GetString();
        if (primeiro.ValueKind == JsonValueKind.Object && primeiro.TryGetProperty("Alphabetic", out var alpha))
            return alpha.GetString();
        return null;
    }

    /// <summary>"Familia^Nome^Meio" → "Familia Nome Meio" (espaços colapsados, trim).</summary>
    private static string? LimparNomePn(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        var limpo = string.Join(' ', bruto.Split(['^', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>
    /// Combina StudyDate ("YYYYMMDD") e StudyTime ("HHMMSS[.ffffff]") do DICOM-JSON
    /// num <see cref="DateTime"/> local-wall-clock. Hora ausente ⇒ 00:00.
    /// </summary>
    private static DateTime? CombinarDataHoraDicom(string? studyDate, string? studyTime)
    {
        if (string.IsNullOrWhiteSpace(studyDate)) return null;
        var d = new string([.. studyDate.Where(char.IsDigit)]);
        if (d.Length < 8) return null;
        if (!int.TryParse(d[..4], out var ano) || !int.TryParse(d[4..6], out var mes) || !int.TryParse(d[6..8], out var dia))
            return null;

        int hh = 0, mm = 0, ss = 0;
        if (!string.IsNullOrWhiteSpace(studyTime))
        {
            var t = new string([.. studyTime.Split('.')[0].Where(char.IsDigit)]);
            if (t.Length >= 2) int.TryParse(t[..2], out hh);
            if (t.Length >= 4) int.TryParse(t[2..4], out mm);
            if (t.Length >= 6) int.TryParse(t[4..6], out ss);
        }

        try { return new DateTime(ano, mes, dia, hh, mm, ss, DateTimeKind.Unspecified); }
        catch { return null; }
    }

    /// <summary>
    /// GET QIDO-RS ESTRITO para a varredura paginada: 204 devolve null (fim legítimo);
    /// falha de transporte, HTTP não-2xx ou JSON inválido LANÇAM. Diferente de
    /// <see cref="ConsultarAsync"/> (tolerante), aqui o chamador precisa distinguir
    /// "PACS fora do ar" de "sem estudos" — senão o resync reporta varredura limpa vazia.
    /// </summary>
    private async Task<JsonElement?> ConsultarPaginaAsync(string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));

        var resposta = await _http.SendAsync(req, ct);
        if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        resposta.EnsureSuccessStatusCode();

        await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
    }

    /// <summary>Faz o GET QIDO-RS e devolve o array JSON (ou null em falha/204).</summary>
    private async Task<JsonElement?> ConsultarAsync(string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QIDO-RS indisponível em {Url}.", url);
            return null;
        }

        if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogWarning("QIDO-RS retornou {Status} em {Url}.", (int)resposta.StatusCode, url);
            return null;
        }

        await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
        try
        {
            return await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Lê o primeiro valor de uma tag DICOM-JSON (ex.: "0020000D").</summary>
    private static string? Tag(JsonElement estudo, string tag)
    {
        if (estudo.ValueKind != JsonValueKind.Object) return null;
        if (!estudo.TryGetProperty(tag, out var campo)) return null;
        if (!campo.TryGetProperty("Value", out var valor) || valor.ValueKind != JsonValueKind.Array || valor.GetArrayLength() == 0)
            return null;
        var primeiro = valor[0];
        return primeiro.ValueKind == JsonValueKind.String ? primeiro.GetString() : primeiro.ToString();
    }
}
