using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Tfd.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.WhatsApp;

/// <summary>
/// Cliente do canal WhatsApp via Automais.Zap (ADR-0044). Monta o corpo no formato da Cloud API
/// e o relay o repassa à Meta intacto. Simula quando a conexão com o Zap não está configurada
/// ou <c>Tfd:WhatsApp:Simular=true</c>.
/// </summary>
public sealed class WhatsAppCliente(
    HttpClient http,
    ITfdConfigService config,
    SmsMaisDbContext db,
    IConfiguration configuration,
    IMemoryCache memoryCache,
    ILogger<WhatsAppCliente> logger) : IWhatsAppCliente
{
    private const string CacheKeyTemplates = "whatsapp:templates";

    public async Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default)
    {
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return [];

        if (memoryCache.TryGetValue(CacheKeyTemplates, out IReadOnlyList<TemplateWhatsApp>? cache) && cache is not null)
            return cache;

        // O catalogo vem do Automais.Zap, recortado pelo que o token do tenant alcanca.
        var url = $"{ctx.ZapBaseUrl.TrimEnd('/')}/v1/templates";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.ZapToken);
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

    /// <summary>
    /// Lê a resposta de <c>GET /v1/templates</c> do Automais.Zap. Só vêm os aprovados; nome,
    /// idioma, corpo e exemplos já chegam prontos — quem conversa com a Meta e abre os
    /// componentes é o relay.
    /// </summary>
    private static IReadOnlyList<TemplateWhatsApp> ParsearTemplates(string corpo)
    {
        var lista = new List<TemplateWhatsApp>();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(corpo); }
        catch { return lista; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("templates", out var data) || data.ValueKind != JsonValueKind.Array)
                return lista;

            foreach (var t in data.EnumerateArray())
            {
                var nome = Texto(t, "nome");
                if (string.IsNullOrEmpty(nome)) continue;

                var idioma = Texto(t, "idioma") ?? "pt_BR";
                var categoria = Texto(t, "categoria") ?? "";
                var corpoTexto = Texto(t, "corpo");

                IReadOnlyList<string> exemplos = [];
                if (t.TryGetProperty("exemplos", out var ex) && ex.ValueKind == JsonValueKind.Array)
                {
                    exemplos = [.. ex.EnumerateArray()
                        .Select(v => v.GetString())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!)];
                }

                lista.Add(new TemplateWhatsApp(
                    nome, idioma, categoria, corpoTexto, ContarParametros(corpoTexto), exemplos));
            }
        }

        return lista;
    }

    private static string? Texto(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

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
        Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = !string.IsNullOrWhiteSpace(conteudoLegivel)
            ? conteudoLegivel!
            : parametros.Count == 0 ? $"[template:{template}]" : $"[template:{template}] {string.Join(" | ", parametros)}";
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
        // O corpo e o da Cloud API, intacto: o Automais.Zap so o embrulha e repassa. Nao ha
        // uma segunda gramatica para manter em dia com a Meta.
        var url = $"{ctx.ZapBaseUrl.TrimEnd('/')}/v1/mensagens";
        var carga = new { phone_number_id = ctx.PhoneNumberId, para = telefone, mensagem = body };
        var msg = NovaMensagem(telefone, template, conteudo, pacienteId);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(carga) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.ZapToken);
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                var erro = ExtrairErroZap(corpo) ?? $"HTTP {(int)resp.StatusCode}: {corpo}";
                msg.Status = StatusMensagemWhatsApp.Falha;
                msg.Conteudo = Truncar($"{conteudo} | erro {(int)resp.StatusCode}: {corpo}");
                msg.ErroMeta = erro.Length <= 500 ? erro : erro[..500];
                db.MensagensWhatsApp.Add(msg);
                await db.SaveChangesAsync(ct);
                logger.LogWarning("WhatsApp envio falhou {Status}: {Corpo}", resp.StatusCode, corpo);
                return new EnvioWhatsAppResultado(false, null, erro);
            }

            msg.WaMessageId = ExtrairWamidZap(corpo);
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

    /// <summary>Resposta do Automais.Zap no sucesso: <c>{"wamid":"..."}</c>.</summary>
    private static string? ExtrairWamidZap(string corpo)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(corpo);
            return doc.RootElement.TryGetProperty("wamid", out var w) ? w.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>Resposta do Automais.Zap no erro: <c>{"erro":"..."}</c>.</summary>
    private static string? ExtrairErroZap(string corpo)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(corpo);
            return doc.RootElement.TryGetProperty("erro", out var e) ? e.GetString() : null;
        }
        catch { return null; }
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
