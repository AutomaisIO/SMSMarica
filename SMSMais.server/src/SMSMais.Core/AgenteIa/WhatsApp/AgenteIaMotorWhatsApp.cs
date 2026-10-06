using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SMSMais.Core.AgenteIa.WhatsApp;

/// <summary>Turno aberto no motor. <paramref name="NovaSessao"/>: não havia sessão viva para o telefone.</summary>
public sealed record TurnoIniciado(string SessaoId, string TurnoId, bool NovaSessao);

/// <summary>O motor já tem um turno rodando nessa sessão — o pedido espera a vez.</summary>
public sealed class TurnoOcupadoException(string? turnoAtual)
    : Exception("Já existe um turno em execução na sessão do WhatsApp.")
{
    public string? TurnoAtual { get; } = turnoAtual;
}

/// <summary>Evento do turno, como o motor grava (text, tool_use, tool_result, result…).</summary>
public sealed record EventoTurno(string Tipo, string? Texto, bool Erro);

/// <param name="Situacao">running | done | error | cancelled | interrupted | timeout.</param>
public sealed record LeituraTurno(string Situacao, string? Erro, IReadOnlyList<EventoTurno> Eventos, int Cursor)
{
    public bool Rodando => Situacao == "running";
}

public sealed record EstadoSessaoWhatsApp(
    string? SessaoId, string? Titulo, DateTime? CriadaEm, DateTime? UltimoUsoEm, int Turnos, bool Rodando);

/// <summary>
/// Fala com o aiengine (127.0.0.1:5085) nos endpoints do canal WhatsApp (ADR-0068). Mesma
/// chave interna e mesmo HttpClient do painel do Agente IA. A identidade vai nos cabeçalhos
/// <c>X-SMSMarica-Usuario-*</c>: é por ela que o motor decide acesso total × somente leitura.
/// </summary>
public interface IAgenteIaMotorWhatsApp
{
    Task<TurnoIniciado> IniciarTurnoAsync(string telefone, string prompt, Guid usuarioId, string usuarioNome, CancellationToken ct);
    Task<LeituraTurno?> LerTurnoAsync(string turnoId, int cursor, CancellationToken ct);
    Task<bool> CancelarTurnoAsync(string turnoId, CancellationToken ct);
    Task<string?> ReiniciarAsync(string telefone, Guid usuarioId, CancellationToken ct);
    Task<EstadoSessaoWhatsApp> EstadoAsync(string telefone, CancellationToken ct);
}

public sealed class AgenteIaMotorWhatsApp(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IAgenteIaMotorWhatsApp
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _baseUrl = (configuration["AgenteIa:BaseUrl"] ?? "http://127.0.0.1:5085").TrimEnd('/');
    private readonly string? _internalKey = configuration["AgenteIa:InternalKey"];

    public async Task<TurnoIniciado> IniciarTurnoAsync(
        string telefone, string prompt, Guid usuarioId, string usuarioNome, CancellationToken ct)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/internal/ai/whatsapp/turns",
            new { telefone, prompt }, usuarioId, usuarioNome, ct);
        if (status == HttpStatusCode.Conflict)
            throw new TurnoOcupadoException(Texto(corpo, "currentTurnId"));
        Garantir(status, corpo);
        return new TurnoIniciado(
            Texto(corpo, "sessionId") ?? string.Empty,
            Texto(corpo, "turnId") ?? throw new HttpRequestException("O motor não devolveu o id do turno."),
            corpo.TryGetProperty("novaSessao", out var n) && n.ValueKind == JsonValueKind.True);
    }

    public async Task<LeituraTurno?> LerTurnoAsync(string turnoId, int cursor, CancellationToken ct)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Get,
            $"/internal/ai/turns/{Uri.EscapeDataString(turnoId)}?cursor={cursor}", null, null, null, ct);
        if (status == HttpStatusCode.NotFound) return null;
        Garantir(status, corpo);

        var eventos = new List<EventoTurno>();
        if (corpo.TryGetProperty("events", out var evs) && evs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in evs.EnumerateArray())
            {
                eventos.Add(new EventoTurno(
                    Texto(e, "type") ?? "?",
                    Texto(e, "text"),
                    e.TryGetProperty("isError", out var er) && er.ValueKind == JsonValueKind.True));
            }
        }
        var novoCursor = corpo.TryGetProperty("cursor", out var c) && c.TryGetInt32(out var ci)
            ? ci : cursor + eventos.Count;
        return new LeituraTurno(Texto(corpo, "status") ?? "running", Texto(corpo, "error"), eventos, novoCursor);
    }

    public async Task<bool> CancelarTurnoAsync(string turnoId, CancellationToken ct)
    {
        var (status, _) = await EnviarAsync(HttpMethod.Post,
            $"/internal/ai/turns/{Uri.EscapeDataString(turnoId)}/cancel", null, null, null, ct);
        return status == HttpStatusCode.OK;
    }

    public async Task<string?> ReiniciarAsync(string telefone, Guid usuarioId, CancellationToken ct)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Post, "/internal/ai/whatsapp/reiniciar",
            new { telefone }, usuarioId, null, ct);
        Garantir(status, corpo);
        return Texto(corpo, "arquivada");
    }

    public async Task<EstadoSessaoWhatsApp> EstadoAsync(string telefone, CancellationToken ct)
    {
        var (status, corpo) = await EnviarAsync(HttpMethod.Get,
            $"/internal/ai/whatsapp/estado?telefone={Uri.EscapeDataString(telefone)}", null, null, null, ct);
        Garantir(status, corpo);
        return new EstadoSessaoWhatsApp(
            Texto(corpo, "sessionId"), Texto(corpo, "titulo"),
            Epoch(corpo, "criadaEm"), Epoch(corpo, "ultimoUsoEm"),
            corpo.TryGetProperty("turnos", out var t) && t.TryGetInt32(out var ti) ? ti : 0,
            corpo.TryGetProperty("running", out var r) && r.ValueKind == JsonValueKind.True);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(
        HttpMethod metodo, string caminho, object? corpo, Guid? usuarioId, string? usuarioNome, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_internalKey))
            throw new InvalidOperationException("AgenteIa:InternalKey não configurada — Agente IA indisponível.");

        var client = httpClientFactory.CreateClient("agente-ia");
        using var req = new HttpRequestMessage(metodo, $"{_baseUrl}{caminho}");
        if (corpo is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(corpo, Json), Encoding.UTF8, MediaTypeNames.Application.Json);
        req.Headers.TryAddWithoutValidation("X-SMSMarica-Internal-Key", _internalKey.Trim());
        if (usuarioId is Guid id)
            req.Headers.TryAddWithoutValidation("X-SMSMarica-Usuario-Id", id.ToString());
        // Percent-encoded: cabeçalho é latin-1 e nome brasileiro tem acento (o motor desfaz).
        if (!string.IsNullOrWhiteSpace(usuarioNome))
            req.Headers.TryAddWithoutValidation("X-SMSMarica-Usuario-Nome", Uri.EscapeDataString(usuarioNome));

        using var res = await client.SendAsync(req, ct);
        var payload = await res.Content.ReadAsStringAsync(ct);
        JsonElement json;
        try { json = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload).RootElement.Clone(); }
        catch (JsonException) { json = JsonDocument.Parse("{}").RootElement.Clone(); }
        return (res.StatusCode, json);
    }

    private static void Garantir(HttpStatusCode status, JsonElement corpo)
    {
        if ((int)status is >= 200 and < 300) return;
        var detalhe = Texto(corpo, "detail") ?? Texto(corpo, "message");
        throw new HttpRequestException(
            $"O motor do Agente IA respondeu {(int)status}{(detalhe is null ? "" : $": {detalhe}")}");
    }

    private static string? Texto(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    /// <summary>O motor guarda tempo como epoch em segundos (float).</summary>
    private static DateTime? Epoch(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetDouble(out var s)
            ? DateTimeOffset.FromUnixTimeMilliseconds((long)(s * 1000)).UtcDateTime : null;
}
