using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Meta;

/// <summary>
/// Fala com a Graph API para a gestão do App e dos WABAs.
///
/// Nada aqui está no caminho do webhook: são chamadas de administração, feitas quando um
/// operador clica na tela. Se a Meta estiver fora do ar, o relay continua entregando.
/// </summary>
public sealed partial class GraphMetaClient(
    HttpClient http,
    IConfiguracaoMetaService config,
    ILogger<GraphMetaClient> logger) : IGraphMetaClient
{
    [GeneratedRegex(@"\{\{(\d{1,3})\}\}")]
    private static partial Regex RegexParametro();

    /// <summary>Variavel no texto, nos DOIS formatos que a Meta aceita: {{1}} e {{nome_paciente}}.</summary>
    [GeneratedRegex(@"\{\{\s*[A-Za-z0-9_]+\s*\}\}")]
    private static partial Regex RegexVariavel();

    // ------------------------------------------------------------------ App

    public async Task<ResultadoMeta<IReadOnlyList<AssinaturaWebhook>>> ObterWebhookDoAppAsync(CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisAppAsync(ct);
        if (erro is not null) return ResultadoMeta<IReadOnlyList<AssinaturaWebhook>>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get, $"{creds!.AppId}/subscriptions", creds.TokenApp!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<IReadOnlyList<AssinaturaWebhook>>.Falha(r.Erro!);

        var lista = new List<AssinaturaWebhook>();
        foreach (var item in Dados(r.Valor!))
        {
            var campos = new List<string>();
            if (item.TryGetProperty("fields", out var fs) && fs.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in fs.EnumerateArray())
                {
                    campos.Add(f.ValueKind == JsonValueKind.Object ? Texto(f, "name") ?? "?" : f.GetString() ?? "?");
                }
            }

            lista.Add(new AssinaturaWebhook(
                Texto(item, "object") ?? "?",
                Texto(item, "callback_url"),
                item.TryGetProperty("active", out var a) && a.ValueKind == JsonValueKind.True,
                campos));
        }

        return ResultadoMeta<IReadOnlyList<AssinaturaWebhook>>.Ok(lista);
    }

    public async Task<ResultadoMeta<bool>> ConfigurarWebhookDoAppAsync(
        string callbackUrl, IReadOnlyList<string> campos, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisAppAsync(ct);
        if (erro is not null) return ResultadoMeta<bool>.Falha(erro);

        if (string.IsNullOrWhiteSpace(creds!.VerifyToken))
        {
            return ResultadoMeta<bool>.Falha(
                "Verify token não configurado. A Meta chama a Callback URL para conferir esse valor antes de aceitar.");
        }

        var corpo = new Dictionary<string, string>
        {
            ["object"] = "whatsapp_business_account",
            ["callback_url"] = callbackUrl,
            ["verify_token"] = creds.VerifyToken!,
            ["fields"] = string.Join(",", campos),
        };

        logger.LogInformation("Configurando webhook do App {App} para {Url}.", creds.AppId, callbackUrl);
        var r = await ChamarAsync(HttpMethod.Post, $"{creds.AppId}/subscriptions", creds.TokenApp!, corpo, ct);
        return r.Sucesso ? ResultadoMeta<bool>.Ok(true) : ResultadoMeta<bool>.Falha(r.Erro!);
    }

    // ----------------------------------------------------------------- WABA

    public async Task<ResultadoMeta<WabaMeta>> ObterWabaAsync(string wabaId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<WabaMeta>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get,
            $"{wabaId}?fields=id,name,account_review_status,currency", creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<WabaMeta>.Falha(r.Erro!);

        var e = r.Valor!;
        return ResultadoMeta<WabaMeta>.Ok(new WabaMeta(
            Texto(e, "id") ?? wabaId, Texto(e, "name"), Texto(e, "account_review_status"), Texto(e, "currency")));
    }

    public async Task<ResultadoMeta<IReadOnlyList<NumeroMeta>>> ListarNumerosAsync(string wabaId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<IReadOnlyList<NumeroMeta>>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get,
            $"{wabaId}/phone_numbers?fields=id,display_phone_number,verified_name,quality_rating,code_verification_status,platform_type&limit=100",
            creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<IReadOnlyList<NumeroMeta>>.Falha(r.Erro!);

        var lista = Dados(r.Valor!).Select(n => new NumeroMeta(
            Texto(n, "id") ?? "?",
            Texto(n, "display_phone_number"),
            Texto(n, "verified_name"),
            Texto(n, "quality_rating"),
            Texto(n, "code_verification_status"),
            Texto(n, "platform_type"))).ToList();

        return ResultadoMeta<IReadOnlyList<NumeroMeta>>.Ok(lista);
    }

    /// <summary>Tudo que a Meta conta sobre o número, incluindo o status de conta oficial.</summary>
    private const string CamposNumero =
        "id,display_phone_number,verified_name,name_status,status,quality_rating,platform_type,"
        + "throughput,code_verification_status,search_visibility,messaging_limit_tier,"
        + "is_official_business_account,is_on_biz_app,official_business_account";

    /// <summary>
    /// Conjunto reduzido, só com campos antigos. A versão da Graph é configurável pelo
    /// operador (Meta → Credenciais); numa versão velha, um campo novo faz a Meta recusar a
    /// chamada INTEIRA com "(#100) nonexisting field" — a tela ficaria vazia por causa de um
    /// campo acessório. Melhor perder duas linhas do diagnóstico do que perder a tela.
    /// </summary>
    private const string CamposNumeroMinimos =
        "id,display_phone_number,verified_name,name_status,status,quality_rating,platform_type,"
        + "code_verification_status,official_business_account";

    public async Task<ResultadoMeta<NumeroDetalheMeta>> ObterNumeroAsync(string phoneNumberId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<NumeroDetalheMeta>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get, $"{phoneNumberId}?fields={CamposNumero}", creds!.TokenSistema!, null, ct);
        if (!r.Sucesso)
        {
            logger.LogInformation("Consulta completa do número {Numero} falhou ({Erro}); tentando o conjunto mínimo.",
                phoneNumberId, r.Erro);
            r = await ChamarAsync(HttpMethod.Get, $"{phoneNumberId}?fields={CamposNumeroMinimos}", creds.TokenSistema!, null, ct);
            if (!r.Sucesso) return ResultadoMeta<NumeroDetalheMeta>.Falha(r.Erro!);
        }

        var e = r.Valor!;
        var oba = e.TryGetProperty("official_business_account", out var o) ? o : default;
        var vazao = e.TryGetProperty("throughput", out var t) ? t : default;

        return ResultadoMeta<NumeroDetalheMeta>.Ok(new NumeroDetalheMeta(
            Texto(e, "id") ?? phoneNumberId,
            Texto(e, "display_phone_number"),
            Texto(e, "verified_name"),
            Texto(e, "name_status"),
            Texto(e, "status"),
            Texto(e, "quality_rating"),
            Texto(e, "platform_type"),
            Texto(vazao, "level"),
            Texto(e, "code_verification_status"),
            Texto(e, "search_visibility"),
            Texto(e, "messaging_limit_tier"),
            Booleano(e, "is_official_business_account"),
            Texto(oba, "oba_status"),
            Booleano(e, "is_on_biz_app"),
            Bonito(e)));
    }

    public async Task<ResultadoMeta<IReadOnlyList<AppInscrito>>> ListarAppsInscritosAsync(string wabaId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<IReadOnlyList<AppInscrito>>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get, $"{wabaId}/subscribed_apps", creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<IReadOnlyList<AppInscrito>>.Falha(r.Erro!);

        var lista = new List<AppInscrito>();
        foreach (var item in Dados(r.Valor!))
        {
            // A Meta aninha o app em whatsapp_business_api_data.
            var alvo = item.TryGetProperty("whatsapp_business_api_data", out var d) ? d : item;
            lista.Add(new AppInscrito(Texto(alvo, "id") ?? "?", Texto(alvo, "name")));
        }

        return ResultadoMeta<IReadOnlyList<AppInscrito>>.Ok(lista);
    }

    public async Task<ResultadoMeta<bool>> InscreverAppNoWabaAsync(string wabaId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<bool>.Falha(erro);

        logger.LogWarning("Inscrevendo o App no WABA {Waba} — muda para onde a Meta entrega os eventos.", wabaId);
        var r = await ChamarAsync(HttpMethod.Post, $"{wabaId}/subscribed_apps", creds!.TokenSistema!, new Dictionary<string, string>(), ct);
        return r.Sucesso ? ResultadoMeta<bool>.Ok(true) : ResultadoMeta<bool>.Falha(r.Erro!);
    }

    public async Task<ResultadoMeta<bool>> DesinscreverAppDoWabaAsync(string wabaId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<bool>.Falha(erro);

        logger.LogWarning("Desinscrevendo o App do WABA {Waba} — para de receber eventos dele.", wabaId);
        var r = await ChamarAsync(HttpMethod.Delete, $"{wabaId}/subscribed_apps", creds!.TokenSistema!, null, ct);
        return r.Sucesso ? ResultadoMeta<bool>.Ok(true) : ResultadoMeta<bool>.Falha(r.Erro!);
    }

    // ------------------------------------------------------------ Templates

    public async Task<ResultadoMeta<IReadOnlyList<TemplateMeta>>> ListarTemplatesAsync(string wabaId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<IReadOnlyList<TemplateMeta>>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get,
            $"{wabaId}/message_templates?fields=id,name,language,category,status,components,rejected_reason&limit=200",
            creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<IReadOnlyList<TemplateMeta>>.Falha(r.Erro!);

        var lista = new List<TemplateMeta>();
        foreach (var t in Dados(r.Valor!))
        {
            string? corpo = null;
            var exemplos = new List<string>();
            CabecalhoTemplateMeta? cabecalho = null;
            if (t.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in comps.EnumerateArray())
                {
                    var tipo = (Texto(c, "type") ?? "").ToUpperInvariant();

                    // O cabecalho vale tanto quanto o corpo: modelo com foto no topo exige o
                    // componente de header em CADA envio, e sem este campo quem envia nao tem
                    // como saber disso antes de a Meta recusar.
                    if (tipo == "HEADER")
                    {
                        cabecalho = LerCabecalho(c);
                        continue;
                    }

                    if (tipo != "BODY") continue;

                    corpo = Texto(c, "text");
                    // example.body_text vem como lista DE LISTAS (um conjunto por variacao);
                    // a primeira basta para pre-preencher a tela de quem vai enviar.
                    if (c.TryGetProperty("example", out var ex)
                        && ex.TryGetProperty("body_text", out var bt)
                        && bt.ValueKind == JsonValueKind.Array && bt.GetArrayLength() > 0
                        && bt[0].ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in bt[0].EnumerateArray())
                        {
                            if (v.ValueKind == JsonValueKind.String) exemplos.Add(v.GetString() ?? "");
                        }
                    }
                }
            }

            var parametros = corpo is null
                ? 0
                : RegexParametro().Matches(corpo).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();

            lista.Add(new TemplateMeta(
                Texto(t, "id") ?? "?",
                Texto(t, "name") ?? "?",
                Texto(t, "language") ?? "?",
                Texto(t, "category") ?? "?",
                Texto(t, "status") ?? "?",
                corpo,
                parametros,
                Texto(t, "rejected_reason"),
                exemplos,
                cabecalho));
        }

        return ResultadoMeta<IReadOnlyList<TemplateMeta>>.Ok(
            lista.OrderBy(x => x.Nome).ThenBy(x => x.Idioma).ToList());
    }

    /// <summary>
    /// Le o componente HEADER de um modelo. O <c>format</c> e o que importa para quem envia;
    /// o <c>example</c> (<c>header_handle</c> para midia, <c>header_text</c> para texto) so
    /// serve de amostra na tela — a URL que a Meta devolve ali e um handle interno dela,
    /// nao um endereco para reenviar.
    /// </summary>
    private static CabecalhoTemplateMeta LerCabecalho(JsonElement c)
    {
        var formato = (Texto(c, "format") ?? "TEXT").ToUpperInvariant();
        var texto = Texto(c, "text");

        string? exemplo = null;
        if (c.TryGetProperty("example", out var ex) && ex.ValueKind == JsonValueKind.Object)
        {
            foreach (var campo in new[] { "header_handle", "header_text" })
            {
                if (!ex.TryGetProperty(campo, out var lista)
                    || lista.ValueKind != JsonValueKind.Array || lista.GetArrayLength() == 0) continue;
                if (lista[0].ValueKind == JsonValueKind.String) exemplo = lista[0].GetString();
                break;
            }
        }

        var parametros = texto is null ? 0 : RegexVariavel().Matches(texto).Count;
        return new CabecalhoTemplateMeta(formato, texto, parametros, exemplo);
    }

    public async Task<ResultadoMeta<string>> CriarTemplateAsync(string wabaId, NovoTemplate template, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<string>.Falha(erro);

        var componentes = new List<object>();

        if (!string.IsNullOrWhiteSpace(template.Cabecalho))
        {
            componentes.Add(new { type = "HEADER", format = "TEXT", text = template.Cabecalho });
        }

        // A Meta EXIGE exemplo para todo {{n}} do corpo; sem isso a submissão volta com erro
        // pouco explicativo. Falta de exemplo vira um placeholder em vez de deixar quebrar.
        var qtd = RegexParametro().Matches(template.Corpo).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        if (qtd > 0)
        {
            var exemplos = Enumerable.Range(0, qtd)
                .Select(i => i < template.ExemplosCorpo.Count && !string.IsNullOrWhiteSpace(template.ExemplosCorpo[i])
                    ? template.ExemplosCorpo[i]
                    : $"exemplo{i + 1}")
                .ToArray();
            componentes.Add(new { type = "BODY", text = template.Corpo, example = new { body_text = new[] { exemplos } } });
        }
        else
        {
            componentes.Add(new { type = "BODY", text = template.Corpo });
        }

        if (!string.IsNullOrWhiteSpace(template.Rodape))
        {
            componentes.Add(new { type = "FOOTER", text = template.Rodape });
        }

        var corpoJson = JsonSerializer.Serialize(new
        {
            name = template.Nome,
            language = template.Idioma,
            category = template.Categoria,
            components = componentes,
        });

        var r = await ChamarJsonAsync(HttpMethod.Post, $"{wabaId}/message_templates", creds!.TokenSistema!, corpoJson, ct);
        if (!r.Sucesso) return ResultadoMeta<string>.Falha(r.Erro!);

        return ResultadoMeta<string>.Ok(Texto(r.Valor!, "id") ?? "(sem id)");
    }

    public async Task<ResultadoMeta<bool>> ExcluirTemplateAsync(string wabaId, string nome, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<bool>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Delete,
            $"{wabaId}/message_templates?name={Uri.EscapeDataString(nome)}", creds!.TokenSistema!, null, ct);
        return r.Sucesso ? ResultadoMeta<bool>.Ok(true) : ResultadoMeta<bool>.Falha(r.Erro!);
    }

    // ------------------------------------------------- Perfil do negócio

    public async Task<ResultadoMeta<PerfilNegocioMeta>> ObterPerfilNegocioAsync(string phoneNumberId, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<PerfilNegocioMeta>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get,
            $"{phoneNumberId}/whatsapp_business_profile?fields=about,address,description,email,profile_picture_url,websites,vertical",
            creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<PerfilNegocioMeta>.Falha(r.Erro!);

        var p = Dados(r.Valor!).FirstOrDefault();
        if (p.ValueKind != JsonValueKind.Object)
        {
            return ResultadoMeta<PerfilNegocioMeta>.Falha("A Meta devolveu o perfil vazio para este número.");
        }

        var sites = new List<string>();
        if (p.TryGetProperty("websites", out var ws) && ws.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in ws.EnumerateArray())
            {
                if (s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString()))
                {
                    sites.Add(s.GetString()!);
                }
            }
        }

        return ResultadoMeta<PerfilNegocioMeta>.Ok(new PerfilNegocioMeta(
            Texto(p, "about"),
            Texto(p, "address"),
            Texto(p, "description"),
            Texto(p, "email"),
            Texto(p, "profile_picture_url"),
            sites,
            Texto(p, "vertical")));
    }

    public async Task<ResultadoMeta<bool>> AtualizarPerfilNegocioAsync(
        string phoneNumberId, AtualizarPerfilNegocio dados, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<bool>.Falha(erro);

        // Campo vazio não é enviado: a Cloud API recusa "about" em branco, e mandar só o que
        // mudou mantém o resto como está — que é o que o operador espera de um formulário.
        var corpo = new Dictionary<string, object> { ["messaging_product"] = "whatsapp" };
        if (!string.IsNullOrWhiteSpace(dados.Sobre)) corpo["about"] = dados.Sobre.Trim();
        if (!string.IsNullOrWhiteSpace(dados.Endereco)) corpo["address"] = dados.Endereco.Trim();
        if (!string.IsNullOrWhiteSpace(dados.Descricao)) corpo["description"] = dados.Descricao.Trim();
        if (!string.IsNullOrWhiteSpace(dados.Email)) corpo["email"] = dados.Email.Trim();
        if (dados.Sites.Count > 0) corpo["websites"] = dados.Sites;
        if (!string.IsNullOrWhiteSpace(dados.Vertical) && dados.Vertical != "UNDEFINED")
        {
            corpo["vertical"] = dados.Vertical;
        }

        logger.LogInformation("Atualizando perfil do negócio do número {Numero}.", phoneNumberId);
        var r = await ChamarJsonAsync(HttpMethod.Post, $"{phoneNumberId}/whatsapp_business_profile",
            creds!.TokenSistema!, JsonSerializer.Serialize(corpo), ct);
        return r.Sucesso ? ResultadoMeta<bool>.Ok(true) : ResultadoMeta<bool>.Falha(r.Erro!);
    }

    public async Task<ResultadoMeta<bool>> AtualizarFotoPerfilAsync(
        string phoneNumberId, byte[] conteudo, string contentType, CancellationToken ct = default)
    {
        // A Upload API só aceita JPEG/PNG para foto de perfil; barrar aqui devolve uma
        // mensagem legível em vez do "(#100)" cru da Meta.
        if (contentType is not ("image/jpeg" or "image/jpg" or "image/png"))
        {
            return ResultadoMeta<bool>.Falha("A foto do perfil precisa ser JPEG ou PNG.");
        }

        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<bool>.Falha(erro);

        // A foto não vai direto no perfil: primeiro sobe pela Resumable Upload API do App,
        // que devolve um handle; é o handle que o perfil aceita. file_name é obrigatório
        // no contrato da Upload API, mesmo que a Meta hoje tolere a ausência.
        var nomeArquivo = contentType == "image/png" ? "perfil.png" : "perfil.jpg";
        var abertura = await ChamarAsync(HttpMethod.Post,
            $"{creds!.AppId}/uploads?file_length={conteudo.Length}&file_type={Uri.EscapeDataString(contentType)}&file_name={nomeArquivo}",
            creds.TokenSistema!, new Dictionary<string, string>(), ct);
        if (!abertura.Sucesso) return ResultadoMeta<bool>.Falha("abrindo o upload: " + abertura.Erro);

        var sessao = Texto(abertura.Valor!, "id");
        if (string.IsNullOrWhiteSpace(sessao))
        {
            return ResultadoMeta<bool>.Falha("A Meta não devolveu o id da sessão de upload.");
        }

        // O envio dos bytes usa o esquema "OAuth" (não "Bearer") e o offset em header — é o
        // contrato da Upload API, diferente do resto da Graph.
        string? handle;
        try
        {
            var baseUrl = (await config.ObterAsync(ct)).BaseUrl.TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{sessao}");
            req.Headers.TryAddWithoutValidation("Authorization", $"OAuth {creds.TokenSistema}");
            req.Headers.TryAddWithoutValidation("file_offset", "0");
            req.Content = new ByteArrayContent(conteudo);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var resp = await http.SendAsync(req, ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "{}" : texto);

            if (!resp.IsSuccessStatusCode)
            {
                var msg = doc.RootElement.TryGetProperty("error", out var err)
                    ? Texto(err, "message") ?? $"HTTP {(int)resp.StatusCode}"
                    : $"HTTP {(int)resp.StatusCode}";
                logger.LogWarning("Upload da foto de perfil recusado: {Erro}", msg);
                return ResultadoMeta<bool>.Falha("enviando a imagem: " + msg);
            }

            handle = Texto(doc.RootElement, "h");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha enviando a foto de perfil.");
            return ResultadoMeta<bool>.Falha(ex is TaskCanceledException ? "timeout enviando a imagem" : ex.Message);
        }

        if (string.IsNullOrWhiteSpace(handle))
        {
            return ResultadoMeta<bool>.Falha("A Meta não devolveu o handle da imagem enviada.");
        }

        logger.LogInformation("Trocando a foto de perfil do número {Numero}.", phoneNumberId);
        var grava = await ChamarJsonAsync(HttpMethod.Post, $"{phoneNumberId}/whatsapp_business_profile",
            creds.TokenSistema!,
            JsonSerializer.Serialize(new { messaging_product = "whatsapp", profile_picture_handle = handle }), ct);
        return grava.Sucesso ? ResultadoMeta<bool>.Ok(true) : ResultadoMeta<bool>.Falha(grava.Erro!);
    }

    // --------------------------------------------------- Métricas da Meta

    public async Task<ResultadoMeta<IReadOnlyList<PontoAnalytics>>> ObterAnalyticsAsync(
        string wabaId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<IReadOnlyList<PontoAnalytics>>.Falha(erro);

        var r = await ChamarAsync(HttpMethod.Get,
            $"{wabaId}?fields=analytics.start({inicio.ToUnixTimeSeconds()}).end({fim.ToUnixTimeSeconds()}).granularity(DAY)",
            creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<IReadOnlyList<PontoAnalytics>>.Falha(r.Erro!);

        var pontos = new List<PontoAnalytics>();
        if (r.Valor!.TryGetProperty("analytics", out var a)
            && a.TryGetProperty("data_points", out var dp) && dp.ValueKind == JsonValueKind.Array)
        {
            foreach (var ponto in dp.EnumerateArray())
            {
                pontos.Add(new PontoAnalytics(
                    DateTimeOffset.FromUnixTimeSeconds(Inteiro(ponto, "start")),
                    Inteiro(ponto, "sent"),
                    Inteiro(ponto, "delivered")));
            }
        }

        return ResultadoMeta<IReadOnlyList<PontoAnalytics>>.Ok(pontos.OrderBy(p => p.Inicio).ToList());
    }

    public async Task<ResultadoMeta<IReadOnlyList<CategoriaCobranca>>> ObterCobrancaAsync(
        string wabaId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var (creds, erro) = await CredenciaisSistemaAsync(ct);
        if (erro is not null) return ResultadoMeta<IReadOnlyList<CategoriaCobranca>>.Falha(erro);

        // pricing_analytics é o campo do modelo por MENSAGEM (jul/2025+); o antigo
        // conversation_analytics reporta o modelo por conversa, que não é mais o cobrado.
        var r = await ChamarAsync(HttpMethod.Get,
            $"{wabaId}?fields=pricing_analytics.start({inicio.ToUnixTimeSeconds()}).end({fim.ToUnixTimeSeconds()})"
            + ".granularity(DAILY).dimensions([\"PRICING_CATEGORY\"])",
            creds!.TokenSistema!, null, ct);
        if (!r.Sucesso) return ResultadoMeta<IReadOnlyList<CategoriaCobranca>>.Falha(r.Erro!);

        // Agregado por categoria: a tela mostra o período inteiro, não o dia a dia.
        var porCategoria = new Dictionary<string, (long Mensagens, decimal Custo)>(StringComparer.OrdinalIgnoreCase);
        if (r.Valor!.TryGetProperty("pricing_analytics", out var pa)
            && pa.TryGetProperty("data", out var dados) && dados.ValueKind == JsonValueKind.Array)
        {
            foreach (var bloco in dados.EnumerateArray())
            {
                if (!bloco.TryGetProperty("data_points", out var dp) || dp.ValueKind != JsonValueKind.Array) continue;
                foreach (var ponto in dp.EnumerateArray())
                {
                    var categoria = Texto(ponto, "pricing_category") ?? "OUTRAS";
                    var atual = porCategoria.GetValueOrDefault(categoria);
                    porCategoria[categoria] = (atual.Mensagens + Inteiro(ponto, "volume"),
                        atual.Custo + Fracionado(ponto, "cost"));
                }
            }
        }

        var lista = porCategoria
            .Select(kv => new CategoriaCobranca(kv.Key, kv.Value.Mensagens, kv.Value.Custo))
            .OrderByDescending(c => c.Mensagens)
            .ToList();

        return ResultadoMeta<IReadOnlyList<CategoriaCobranca>>.Ok(lista);
    }

    // ---------------------------------------------------------------- Apoio

    private async Task<(CredenciaisMeta? Creds, string? Erro)> CredenciaisSistemaAsync(CancellationToken ct)
    {
        var c = await config.ObterAsync(ct);
        return c.PodeGerenciar
            ? (c, null)
            : (null, "Falta o App ID ou o token do System User. Configure em Meta → Credenciais.");
    }

    private async Task<(CredenciaisMeta? Creds, string? Erro)> CredenciaisAppAsync(CancellationToken ct)
    {
        var c = await config.ObterAsync(ct);
        return c.TokenApp is not null
            ? (c, null)
            : (null, "Falta o App ID ou o App Secret. Configure em Meta → Credenciais.");
    }

    private async Task<ResultadoMeta<JsonElement>> ChamarAsync(
        HttpMethod metodo, string caminho, string token, IDictionary<string, string>? formulario, CancellationToken ct)
    {
        HttpContent? conteudo = formulario is null ? null : new FormUrlEncodedContent(formulario);
        return await EnviarAsync(metodo, caminho, token, conteudo, ct);
    }

    private async Task<ResultadoMeta<JsonElement>> ChamarJsonAsync(
        HttpMethod metodo, string caminho, string token, string json, CancellationToken ct)
        => await EnviarAsync(metodo, caminho, token, new StringContent(json, Encoding.UTF8, "application/json"), ct);

    private async Task<ResultadoMeta<JsonElement>> EnviarAsync(
        HttpMethod metodo, string caminho, string token, HttpContent? conteudo, CancellationToken ct)
    {
        var creds = await config.ObterAsync(ct);
        var url = creds.BaseUrl.TrimEnd('/') + "/" + caminho;

        try
        {
            using var req = new HttpRequestMessage(metodo, url) { Content = conteudo };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await http.SendAsync(req, ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "{}" : texto);
            var raiz = doc.RootElement.Clone();

            if (resp.IsSuccessStatusCode) return ResultadoMeta<JsonElement>.Ok(raiz);

            var msg = raiz.TryGetProperty("error", out var err)
                ? $"{Texto(err, "message")} (code {Texto(err, "code")}{(Texto(err, "error_subcode") is { } sc ? "/" + sc : "")})"
                : $"HTTP {(int)resp.StatusCode}";

            logger.LogWarning("Graph API recusou {Metodo} {Caminho}: {Erro}", metodo, caminho, msg);
            return ResultadoMeta<JsonElement>.Falha(msg);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha chamando a Graph API em {Caminho}.", caminho);
            return ResultadoMeta<JsonElement>.Falha(ex is TaskCanceledException ? "timeout falando com a Meta" : ex.Message);
        }
    }

    private static IEnumerable<JsonElement> Dados(JsonElement raiz)
        => raiz.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array
            ? d.EnumerateArray()
            : [];

    private static string? Texto(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                _ => null,
            }
            : null;

    /// <summary>
    /// Nulo quando a Meta não mandou o campo — que é diferente de <c>false</c>. "Não é conta
    /// oficial" e "a Meta não respondeu se é" não podem virar a mesma coisa na tela.
    /// </summary>
    private static bool? Booleano(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => (bool?)null,
            }
            : null;

    private static long Inteiro(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
           && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)
            ? n
            : 0;

    private static decimal Fracionado(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
           && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n)
            ? n
            : 0m;

    private static readonly JsonSerializerOptions Identado = new() { WriteIndented = true };

    private static string Bonito(JsonElement e) => JsonSerializer.Serialize(e, Identado);
}
