using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Tfd.Configuracao;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Tfd;

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
    ILogger<WhatsAppCliente> logger) : IWhatsAppCliente
{
    public async Task<EnvioWhatsAppResultado> EnviarTextoAsync(
        string telefone, string texto, Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template: null, texto, sessaoId, pacienteId, ct);

        object body = new { messaging_product = "whatsapp", to = fone, type = "text", text = new { body = texto } };
        return await EnviarRealAsync(ctx, body, fone, template: null, conteudo: texto, sessaoId, pacienteId, ct);
    }

    public async Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
        string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
        Guid? sessaoId = null, Guid? pacienteId = null, CancellationToken ct = default)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = parametros.Count == 0 ? $"[template:{template}]" : $"[template:{template}] {string.Join(" | ", parametros)}";
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template, conteudo, sessaoId, pacienteId, ct);

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
        return await EnviarRealAsync(ctx, body, fone, template, conteudo, sessaoId, pacienteId, ct);
    }

    private async Task<TfdWhatsAppContexto?> ObterContextoOuNuloAsync(CancellationToken ct)
    {
        if (configuration.GetValue("Tfd:WhatsApp:Simular", defaultValue: false)) return null;
        try { return await config.ObterWhatsAppContextoAsync(ct); }
        catch (ValidacaoException) { return null; } // sem conta configurada → simula
    }

    private async Task<EnvioWhatsAppResultado> SimularAsync(
        string telefone, string? template, string conteudo, Guid? sessaoId, Guid? pacienteId, CancellationToken ct)
    {
        var wamid = "simulado-" + Guid.CreateVersion7().ToString("N");
        var msg = NovaMensagem(telefone, template, Truncar($"[SIMULADO] {conteudo}"), sessaoId, pacienteId);
        msg.WaMessageId = wamid;
        db.MensagensWhatsApp.Add(msg);
        try { await db.SaveChangesAsync(ct); } catch { /* best-effort */ }
        logger.LogInformation("[WhatsApp SIMULADO] → {Telefone}: {Conteudo}", telefone, conteudo);
        return new EnvioWhatsAppResultado(true, wamid, null);
    }

    private async Task<EnvioWhatsAppResultado> EnviarRealAsync(
        TfdWhatsAppContexto ctx, object body, string telefone, string? template, string conteudo,
        Guid? sessaoId, Guid? pacienteId, CancellationToken ct)
    {
        var url = $"{ctx.BaseUrl.TrimEnd('/')}/{ctx.PhoneNumberId}/messages";
        var msg = NovaMensagem(telefone, template, conteudo, sessaoId, pacienteId);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.Token);
            using var resp = await http.SendAsync(req, ct);
            var corpo = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                msg.Status = StatusMensagemWhatsApp.Falha;
                msg.Conteudo = Truncar($"{conteudo} | erro {(int)resp.StatusCode}: {corpo}");
                db.MensagensWhatsApp.Add(msg);
                await db.SaveChangesAsync(ct);
                logger.LogWarning("WhatsApp envio falhou {Status}: {Corpo}", resp.StatusCode, corpo);
                return new EnvioWhatsAppResultado(false, null, corpo);
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

    private static MensagemWhatsApp NovaMensagem(string telefone, string? template, string conteudo, Guid? sessaoId, Guid? pacienteId) => new()
    {
        Id = Guid.CreateVersion7(),
        SessaoId = sessaoId,
        PacienteId = pacienteId,
        Telefone = telefone,
        Template = template,
        Direcao = DirecaoMensagem.Saida,
        Conteudo = conteudo,
        Status = StatusMensagemWhatsApp.Enviada,
        OcorridoEm = DateTime.UtcNow,
        CriadoEm = DateTime.UtcNow,
    };

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
