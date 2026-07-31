using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Tfd.Configuracao;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Notificacoes;

namespace SMSMarica.Core.Notificacoes.WhatsApp;

/// <summary>
/// Cliente do WhatsApp Cloud API (Meta). Enquanto não há conta Meta verificada, opera em
/// <b>modo simulado</b>: registra/loga a mensagem que seria enviada e devolve sucesso (não
/// chama a Meta). Simula quando a integração não está configurada ou quando
/// <c>Tfd:WhatsApp:Simular=true</c>. Configurada a conta, passa a enviar de verdade sozinho.
/// </summary>
public sealed class WhatsAppCliente(
    HttpClient http,
    ITfdConfigService config,
    SmsMaricaDbContext db,
    IConfiguration configuration,
    IMemoryCache memoryCache,
    ILogger<WhatsAppCliente> logger) : IWhatsAppCliente
{
    private const string CacheKeyTemplates = "whatsapp:templates";

    public async Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default)
    {
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null || string.IsNullOrWhiteSpace(ctx.WabaId)) return [];

        if (memoryCache.TryGetValue(CacheKeyTemplates, out IReadOnlyList<TemplateWhatsApp>? cache) && cache is not null)
            return cache;

        var url = $"{ctx.BaseUrl.TrimEnd('/')}/{ctx.WabaId}/message_templates?limit=200";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.Token);
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("WhatsApp listar templates falhou {Status}: {Corpo}", resp.StatusCode, corpo);
                return [];
            }

            var lista = ParsearTemplates(corpo);
            memoryCache.Set(CacheKeyTemplates, lista, TimeSpan.FromMinutes(5));
            return lista;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao listar templates do WhatsApp.");
            return [];
        }
    }

    private static IReadOnlyList<TemplateWhatsApp> ParsearTemplates(string corpo)
    {
        var lista = new List<TemplateWhatsApp>();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(corpo); }
        catch { return lista; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return lista;

            foreach (var t in data.EnumerateArray())
            {
                var status = t.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (!string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase)) continue;

                var nome = t.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(nome)) continue;

                var idioma = (t.TryGetProperty("language", out var l) ? l.GetString() : null) ?? "pt_BR";
                var categoria = (t.TryGetProperty("category", out var c) ? c.GetString() : null) ?? "";

                string? corpoTexto = null;
                IReadOnlyList<string> exemplos = [];
                if (t.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var comp in comps.EnumerateArray())
                    {
                        if (comp.TryGetProperty("type", out var tp)
                            && string.Equals(tp.GetString(), "BODY", StringComparison.OrdinalIgnoreCase)
                            && comp.TryGetProperty("text", out var txt))
                        {
                            corpoTexto = txt.GetString();
                            exemplos = ExtrairExemplos(comp);
                            break;
                        }
                    }
                }

                lista.Add(new TemplateWhatsApp(
                    nome, idioma, categoria, corpoTexto, ContarParametros(corpoTexto), exemplos));
            }
        }

        return lista;
    }

    /// <summary>
    /// Lê <c>example.body_text</c> do componente BODY — a Meta entrega uma lista de listas
    /// (um conjunto de exemplos por variável); usamos o primeiro conjunto.
    /// </summary>
    private static IReadOnlyList<string> ExtrairExemplos(JsonElement componenteBody)
    {
        if (!componenteBody.TryGetProperty("example", out var ex)
            || !ex.TryGetProperty("body_text", out var bt)
            || bt.ValueKind != JsonValueKind.Array)
            return [];

        foreach (var conjunto in bt.EnumerateArray())
        {
            if (conjunto.ValueKind != JsonValueKind.Array) continue;
            return [.. conjunto.EnumerateArray()
                .Select(v => v.GetString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)];
        }

        return [];
    }

    private static int ContarParametros(string? corpo)
    {
        if (string.IsNullOrEmpty(corpo)) return 0;
        var max = 0;
        foreach (Match m in Regex.Matches(corpo, @"\{\{(\d+)\}\}"))
            if (int.TryParse(m.Groups[1].Value, out var idx) && idx > max) max = idx;
        return max;
    }

    public async Task<EnvioWhatsAppResultado> EnviarTextoAsync(
        string telefone, string texto, Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template: null, texto, pacienteId, ct);

        object body = new { messaging_product = "whatsapp", to = fone, type = "text", text = new { body = texto } };
        return await EnviarRealAsync(ctx, body, fone, template: null, conteudo: texto, pacienteId, ct);
    }

    public async Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
        string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
        Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = parametros.Count == 0 ? $"[template:{template}]" : $"[template:{template}] {string.Join(" | ", parametros)}";
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template, conteudo, pacienteId, ct);

        object[]? components = parametros.Count == 0
            ? null
            : [new { type = "body", parameters = parametros.Select(p => new { type = "text", text = p }).ToArray() }];
        object body = new
        {
            messaging_product = "whatsapp",
            to = fone,
            type = "template",
            template = new { name = template, language = new { code = idiomaBcp47 }, components },
        };
        return await EnviarRealAsync(ctx, body, fone, template, conteudo, pacienteId, ct);
    }

    public async Task<EnvioWhatsAppResultado> EnviarTemplateComBotoesAsync(
        string telefone, string template, string idiomaBcp47,
        IReadOnlyList<string> parametrosBody, IReadOnlyList<BotaoTemplateWhatsApp> botoes,
        Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = parametrosBody.Count == 0
            ? $"[template:{template}]"
            : $"[template:{template}] {string.Join(" | ", parametrosBody)}";
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template, conteudo, pacienteId, ct);

        var components = new List<object>();
        if (parametrosBody.Count > 0)
            components.Add(new { type = "body", parameters = parametrosBody.Select(p => new { type = "text", text = p }).ToArray() });
        for (var i = 0; i < botoes.Count; i++)
        {
            var b = botoes[i];
            components.Add(b.Tipo == TipoBotaoTemplate.Url
                ? new
                {
                    type = "button",
                    sub_type = "url",
                    index = i.ToString(),
                    parameters = new object[] { new { type = "text", text = b.Valor } },
                }
                : new
                {
                    type = "button",
                    sub_type = "quick_reply",
                    index = i.ToString(),
                    parameters = new object[] { new { type = "payload", payload = b.Valor } },
                });
        }

        object body = new
        {
            messaging_product = "whatsapp",
            to = fone,
            type = "template",
            template = new { name = template, language = new { code = idiomaBcp47 }, components = components.ToArray() },
        };
        return await EnviarRealAsync(ctx, body, fone, template, conteudo, pacienteId, ct);
    }

    public async Task<EnvioWhatsAppResultado> EnviarInterativoBotoesAsync(
        string telefone, string texto, IReadOnlyList<BotaoInterativoWhatsApp> botoes,
        Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = $"{texto} [{string.Join(" / ", botoes.Select(b => b.Titulo))}]";
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template: null, conteudo, pacienteId, ct);

        object body = new
        {
            messaging_product = "whatsapp",
            to = fone,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = texto },
                action = new
                {
                    buttons = botoes.Select(b => new { type = "reply", reply = new { id = b.Id, title = b.Titulo } }).ToArray(),
                },
            },
        };
        return await EnviarRealAsync(ctx, body, fone, template: null, conteudo, pacienteId, ct);
    }

    public async Task<EnvioWhatsAppResultado> EnviarTemplateAutenticacaoAsync(
        string telefone, string template, string idiomaBcp47, string codigo,
        Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        // Auditoria sem o código em claro (é credencial de uso único).
        var conteudo = $"[template:{template}] código de acesso";
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template, conteudo, pacienteId, ct);

        object[] components =
        [
            new { type = "body", parameters = new[] { new { type = "text", text = codigo } } },
            new
            {
                type = "button",
                sub_type = "url",
                index = "0",
                parameters = new[] { new { type = "text", text = codigo } },
            },
        ];
        object body = new
        {
            messaging_product = "whatsapp",
            to = fone,
            type = "template",
            template = new { name = template, language = new { code = idiomaBcp47 }, components },
        };
        return await EnviarRealAsync(ctx, body, fone, template, conteudo, pacienteId, ct);
    }

    private async Task<TfdWhatsAppContexto?> ObterContextoOuNuloAsync(CancellationToken ct)
    {
        if (configuration.GetValue("Tfd:WhatsApp:Simular", defaultValue: false)) return null;
        try { return await config.ObterWhatsAppContextoAsync(ct); }
        catch (ValidacaoException) { return null; } // sem conta configurada → simula
    }

    private async Task<EnvioWhatsAppResultado> SimularAsync(
        string telefone, string? template, string conteudo, Guid? pacienteId, CancellationToken ct)
    {
        var wamid = "simulado-" + Guid.CreateVersion7().ToString("N");
        var msg = NovaMensagem(telefone, template, Truncar($"[SIMULADO] {conteudo}"), pacienteId);
        msg.WaMessageId = wamid;
        db.MensagensWhatsApp.Add(msg);
        try { await db.SaveChangesAsync(ct); } catch { /* best-effort */ }
        logger.LogInformation("[WhatsApp SIMULADO] → {Telefone}: {Conteudo}", telefone, conteudo);
        return new EnvioWhatsAppResultado(true, wamid, null);
    }

    private async Task<EnvioWhatsAppResultado> EnviarRealAsync(
        TfdWhatsAppContexto ctx, object body, string telefone, string? template, string conteudo,
        Guid? pacienteId, CancellationToken ct)
    {
        var url = $"{ctx.BaseUrl.TrimEnd('/')}/{ctx.PhoneNumberId}/messages";
        var msg = NovaMensagem(telefone, template, conteudo, pacienteId);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.Token);
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                var erro = ExtrairErroMeta(corpo) ?? $"HTTP {(int)resp.StatusCode}: {corpo}";
                msg.Status = StatusMensagemWhatsApp.Falha;
                msg.Conteudo = Truncar($"{conteudo} | erro {(int)resp.StatusCode}: {corpo}");
                msg.ErroMeta = erro.Length <= 500 ? erro : erro[..500];
                db.MensagensWhatsApp.Add(msg);
                await db.SaveChangesAsync(ct);
                logger.LogWarning("WhatsApp envio falhou {Status}: {Corpo}", resp.StatusCode, corpo);
                return new EnvioWhatsAppResultado(false, null, erro);
            }

            msg.WaMessageId = ExtrairWamid(corpo);
            db.MensagensWhatsApp.Add(msg);
            await db.SaveChangesAsync(ct);
            return new EnvioWhatsAppResultado(true, msg.WaMessageId, null);
        }
        catch (Exception ex)
        {
            msg.Status = StatusMensagemWhatsApp.Falha;
            msg.Conteudo = Truncar($"{conteudo} | erro: {ex.Message}");
            try { db.MensagensWhatsApp.Add(msg); await db.SaveChangesAsync(ct); } catch { /* best-effort */ }
            logger.LogWarning(ex, "Falha de rede ao enviar WhatsApp.");
            return new EnvioWhatsAppResultado(false, null, ex.Message);
        }
    }

    private static MensagemWhatsApp NovaMensagem(string telefone, string? template, string conteudo, Guid? pacienteId) => new()
    {
        Id = Guid.CreateVersion7(),
        PacienteId = pacienteId,
        Telefone = telefone,
        Template = template,
        Direcao = DirecaoMensagem.Saida,
        Conteudo = conteudo,
        Status = StatusMensagemWhatsApp.Enviada,
        OcorridoEm = DateTime.UtcNow,
        CriadoEm = DateTime.UtcNow,
    };

    /// <summary>"(code) message — details" a partir do envelope de erro do Graph API; null se o corpo não for esse formato.</summary>
    internal static string? ExtrairErroMeta(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (!doc.RootElement.TryGetProperty("error", out var e)) return null;
            var code = e.TryGetProperty("code", out var c) ? c.ToString() : null;
            var message = e.TryGetProperty("message", out var m) ? m.GetString() : null;
            var details = e.TryGetProperty("error_data", out var ed) && ed.TryGetProperty("details", out var d)
                ? d.GetString() : null;
            if (message is null && code is null) return null;
            var txt = $"({code}) {message}";
            return string.IsNullOrEmpty(details) ? txt : $"{txt} — {details}";
        }
        catch { return null; }
    }

    private static string? ExtrairWamid(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array
                && msgs.GetArrayLength() > 0 && msgs[0].TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }
        catch { /* corpo inesperado */ }
        return null;
    }

    /// <summary>Só dígitos, com DDI Brasil (55) quando vier sem código de país.</summary>
    private static string NormalizarTelefone(string telefone)
    {
        var d = new string([.. telefone.Where(char.IsDigit)]);
        if (d.Length <= 11 && !d.StartsWith("55")) d = "55" + d;
        return d;
    }

    private static string Truncar(string s) => s.Length <= 1000 ? s : s[..1000];
}
