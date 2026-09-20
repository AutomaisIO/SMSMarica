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
    PendenciasCadastro.IContatoNegadoService contatosNegados,
    Microsoft.Extensions.Options.IOptions<Comunicacao.ComunicacaoPacienteOptions> comunicacaoOptions,
    ILogger<WhatsAppCliente> logger) : IWhatsAppCliente
{
    /// <summary>
    /// Componente de cabeçalho para modelo com FOTO no topo. A imagem do modelo aprovado é apenas
    /// exemplo: cada envio precisa mandar a sua, senão a Meta recusa com 132012.
    /// Modelo sem imagem (ou sem URL configurada) não leva header nenhum.
    /// </summary>
    private object? CabecalhoImagem(string template)
    {
        var mapa = comunicacaoOptions.Value.ImagensCabecalho;
        if (mapa is null || !mapa.TryGetValue(template, out var url) || string.IsNullOrWhiteSpace(url))
            return null;

        return new
        {
            type = "header",
            parameters = new object[] { new { type = "image", image = new { link = url } } },
        };
    }

    private const string CacheKeyTemplates = "whatsapp:templates";

    /// <summary>
    /// Guarda de LGPD, no único ponto por onde tudo sai: mensagem que o SISTEMA inicia nunca vai
    /// para um número cujo dono já disse que não conhece aquele paciente. A tentativa fica
    /// registrada (mensagem com o motivo) para a recepção tratar depois.
    /// </summary>
    private async Task<EnvioWhatsAppResultado?> BloqueioAsync(
        OrigemEnvioWhatsApp origem, string telefone, string? template, string conteudo,
        Guid? pacienteId, CancellationToken ct)
    {
        if (origem != OrigemEnvioWhatsApp.Automatico) return null;
        if (!await contatosNegados.BloqueadoAsync(telefone, pacienteId, ct)) return null;

        const string motivo = "BLOQUEADO (LGPD): número marcado como inválido para este paciente — "
            + "quem atende disse que não o conhece. Corrija o contato no cadastro para liberar.";
        var msg = NovaMensagem(telefone, template, Truncar($"[BLOQUEADO] {conteudo}"), pacienteId);
        msg.Status = StatusMensagemWhatsApp.Falha;
        msg.ErroMeta = motivo;
        db.MensagensWhatsApp.Add(msg);
        try { await db.SaveChangesAsync(ct); } catch { /* best-effort: o bloqueio vale mesmo sem registro */ }

        logger.LogWarning("Envio automático bloqueado (contato negado) para …{Fone4} / paciente {Paciente}.",
            telefone.Length <= 4 ? telefone : telefone[^4..], pacienteId);
        return new EnvioWhatsAppResultado(false, null, $"{BloqueioEnvioWhatsApp.CodigoNumeroNegado}: {motivo}");
    }

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
    /// Conteúdo gravado na thread para um envio de template: (1) texto explícito do chamador;
    /// (2) senão, MATERIALIZA o corpo APROVADO do catálogo da Meta (cache 5 min) preenchendo
    /// <c>{{n}}</c> com os parâmetros — operador e robô leem o que o cidadão recebeu;
    /// (3) último recurso, o marcador técnico <c>[template:nome] p1 | p2</c>.
    /// OTP/autenticação não passa por aqui (método próprio; código nunca vai em claro).
    /// </summary>
    private async Task<string> ConteudoDaThreadAsync(
        string template, IReadOnlyList<string> parametros, string? conteudoLegivel, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(conteudoLegivel)) return conteudoLegivel!;
        try
        {
            var corpo = (await ListarTemplatesAsync(ct))
                .FirstOrDefault(t => string.Equals(t.Nome, template, StringComparison.OrdinalIgnoreCase))?.Corpo;
            if (!string.IsNullOrWhiteSpace(corpo))
            {
                var texto = corpo!;
                for (var i = 0; i < parametros.Count; i++)
                    texto = texto.Replace("{{" + (i + 1) + "}}", parametros[i]);
                return texto;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao materializar o template {Template} para a thread.", template);
        }
        return parametros.Count == 0
            ? $"[template:{template}]"
            : $"[template:{template}] {string.Join(" | ", parametros)}";
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

                var variaveis = VariaveisDoCorpo(corpoTexto);
                lista.Add(new TemplateWhatsApp(
                    nome, idioma, categoria, corpoTexto, variaveis.Count, exemplos, variaveis));
            }
        }

        return lista;
    }

    private static string? Texto(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>
    /// As variáveis do corpo, na ordem em que aparecem e sem repetir. Aceita os DOIS formatos que
    /// a Meta permite: numerado (<c>{{1}}</c>) e nomeado (<c>{{nome_paciente}}</c>). Antes só o
    /// numerado era reconhecido — modelo nomeado contava zero variáveis e o envio voltava
    /// <c>132012 Parameter format does not match</c>.
    /// </summary>
    private static IReadOnlyList<string> VariaveisDoCorpo(string? corpo)
    {
        if (string.IsNullOrEmpty(corpo)) return [];

        var vistas = new List<string>();
        foreach (Match m in Regex.Matches(corpo, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}"))
        {
            var v = m.Groups[1].Value;
            if (!vistas.Contains(v, StringComparer.OrdinalIgnoreCase)) vistas.Add(v);
        }

        // Numerado: a ordem é a dos índices, não a da leitura ({{2}} pode vir antes de {{1}}).
        if (vistas.Count > 0 && vistas.All(v => v.All(char.IsAsciiDigit)))
            return [.. vistas.OrderBy(v => int.Parse(v, System.Globalization.CultureInfo.InvariantCulture))];

        return vistas;
    }

    /// <summary>
    /// Monta os parâmetros do corpo no formato que o modelo APROVADO espera: nomeado leva
    /// <c>parameter_name</c>; numerado vai posicional. Catálogo indisponível (relay fora, modo
    /// simulado) cai no posicional, que é o formato antigo.
    /// </summary>
    private async Task<object[]> ParametrosBodyAsync(
        string template, IReadOnlyList<string> valores, CancellationToken ct)
    {
        IReadOnlyList<string>? nomes = null;
        try
        {
            var modelo = (await ListarTemplatesAsync(ct))
                .FirstOrDefault(t => string.Equals(t.Nome, template, StringComparison.OrdinalIgnoreCase));
            if (modelo is { Nomeadas: true }) nomes = modelo.Variaveis;
        }
        catch { /* sem catálogo, segue posicional */ }

        if (nomes is null)
            return [.. valores.Select(object (p) => new { type = "text", text = p })];

        return [.. valores.Select(object (p, i) => new
        {
            type = "text",
            parameter_name = i < nomes.Count ? nomes[i] : $"var{i + 1}",
            text = p,
        })];
    }

    public async Task<EnvioWhatsAppResultado> EnviarTextoAsync(
        string telefone, string texto, Guid? pacienteId = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
    {
        var fone = NormalizarTelefone(telefone);
        if (await BloqueioAsync(origem, fone, null, texto, pacienteId, ct) is { } bloqueio) return bloqueio;
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template: null, texto, pacienteId, ct);

        object body = new { messaging_product = "whatsapp", to = fone, type = "text", text = new { body = texto } };
        return await EnviarRealAsync(ctx, body, fone, template: null, conteudo: texto, pacienteId, ct);
    }

    public async Task<EnvioWhatsAppResultado> EnviarTemplateAsync(
        string telefone, string template, string idiomaBcp47, IReadOnlyList<string> parametros,
        Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = await ConteudoDaThreadAsync(template, parametros, conteudoLegivel, ct);
        if (await BloqueioAsync(origem, fone, template, conteudo, pacienteId, ct) is { } bloqueio) return bloqueio;
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template, conteudo, pacienteId, ct);

        var componentes = new List<object>();
        if (CabecalhoImagem(template) is { } cabecalho) componentes.Add(cabecalho);
        if (parametros.Count > 0)
            componentes.Add(new { type = "body", parameters = await ParametrosBodyAsync(template, parametros, ct) });
        object[]? components = componentes.Count == 0 ? null : [.. componentes];
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
        Guid? pacienteId = null, string? conteudoLegivel = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = await ConteudoDaThreadAsync(template, parametrosBody, conteudoLegivel, ct);
        if (await BloqueioAsync(origem, fone, template, conteudo, pacienteId, ct) is { } bloqueio) return bloqueio;
        var ctx = await ObterContextoOuNuloAsync(ct);
        if (ctx is null) return await SimularAsync(fone, template, conteudo, pacienteId, ct);

        var components = new List<object>();
        if (CabecalhoImagem(template) is { } cabecalhoImagem) components.Add(cabecalhoImagem);
        if (parametrosBody.Count > 0)
            components.Add(new { type = "body", parameters = await ParametrosBodyAsync(template, parametrosBody, ct) });
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
        Guid? pacienteId = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
    {
        var fone = NormalizarTelefone(telefone);
        var conteudo = $"{texto} [{string.Join(" / ", botoes.Select(b => b.Titulo))}]";
        if (await BloqueioAsync(origem, fone, null, conteudo, pacienteId, ct) is { } bloqueio) return bloqueio;
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
        Guid? pacienteId = null, CancellationToken ct = default,
        OrigemEnvioWhatsApp origem = OrigemEnvioWhatsApp.Automatico)
    {
        var fone = NormalizarTelefone(telefone);
        // Auditoria sem o código em claro (é credencial de uso único).
        var conteudo = $"[template:{template}] código de acesso";
        if (await BloqueioAsync(origem, fone, template, conteudo, pacienteId, ct) is { } bloqueio) return bloqueio;
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
