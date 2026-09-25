using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Laudos.Assinatura.Nuvem;

/// <summary>
/// Implementação HTTP da API IntegraICP v3 (ADR-0061).
///
/// <para>
/// <b>Contrato conhecido só em parte.</b> A documentação pública bloqueia acesso fora de
/// navegador e não há ambiente de homologação. O que está aqui vem do spike de 04/08/2026:
/// os três endpoints, os nomes <c>subject_key</c>/<c>secret_data</c>/<c>encodedX509</c>/
/// <c>contentDigest</c>/<c>signaturePolicy</c>/<c>signedContent</c> e o campo
/// <c>unavailables</c>. O formato exato da lista de autorizações não foi fixado, então a
/// leitura dela é tolerante (procura a URL de autorização no primeiro item que tiver uma).
/// A semântica do RAW é conferida no serviço, que verifica a assinatura com a chave pública
/// antes de embutir — se o provedor fizer diferente, o job falha com mensagem clara em vez
/// de gerar PDF inválido.
/// </para>
/// </summary>
public sealed class IntegraIcpClient(
    HttpClient http,
    IOptions<IntegraIcpOptions> options,
    ILogger<IntegraIcpClient> logger) : IIntegraIcpClient
{
    private readonly IntegraIcpOptions _opt = options.Value;

    private string Prefixo => $"c/{Uri.EscapeDataString(_opt.Canal ?? string.Empty)}/icp/v3";

    public async Task<string> IniciarAutorizacaoAsync(
        string cpf, string urlRetorno, string codeChallenge, CancellationToken cancellationToken = default)
    {
        GarantirHabilitado();
        var consulta = new StringBuilder($"{Prefixo}/authentications")
            .Append("?subject_key=").Append(Uri.EscapeDataString(cpf))
            .Append("&callback_uri=").Append(Uri.EscapeDataString(urlRetorno))
            .Append("&secret_data=").Append(Uri.EscapeDataString(codeChallenge))
            .Append("&secret_method=S256")
            // Sem clearance_lifetime o PSC devolve expiração igual ao pedido (achado do spike).
            .Append("&clearance_lifetime=").Append(_opt.AutorizacaoVidaSegundos)
            .Append("&credential_lifetime=").Append(_opt.CredencialVidaSegundos)
            .ToString();

        using var doc = await EnviarAsync(HttpMethod.Get, consulta, null, "authentications", cancellationToken);

        var url = AcharUrlAutorizacao(doc.RootElement);
        logger.LogInformation("IntegraICP: authentications ok (status {Status}; url de autorização {Achou}; campos: {Campos}).",
            LerString(doc.RootElement, "currentStatus") ?? "(sem status)", url is null ? "AUSENTE" : "encontrada",
            Campos(doc.RootElement));
        if (url is not null) return url;

        var motivo = LerIndisponiveis(doc.RootElement);
        logger.LogWarning("IntegraICP: nenhuma autorização disponível para o CPF (motivos: {Motivos}).", motivo ?? "(nenhum informado)");
        throw new ConflitoException("assinatura.nuvem_sem_certificado",
            "Não encontramos certificado em nuvem (VIDaaS) ativo para o CPF deste médico." +
            (motivo is null ? string.Empty : $" Retorno do provedor: {motivo}."));
    }

    public async Task<byte[]> ObterCertificadoAsync(
        string credencialId, string codeVerifier, CancellationToken cancellationToken = default)
    {
        GarantirHabilitado();
        var caminho = $"{Prefixo}/credentials/{Uri.EscapeDataString(credencialId)}" +
                      $"?secret_data={Uri.EscapeDataString(codeVerifier)}";
        using var doc = await EnviarAsync(HttpMethod.Get, caminho, null, "credentials", cancellationToken);

        var pem = LerString(doc.RootElement, "encodedX509");
        if (pem is null)
        {
            // Só os nomes dos campos: o valor traz o certificado (nome e CPF do médico).
            logger.LogWarning("IntegraICP: /credentials sem encodedX509 (campos: {Campos}).", Campos(doc.RootElement));
            throw new ConflitoException("assinatura.nuvem_sem_certificado",
                "O provedor não devolveu o certificado da credencial autorizada.");
        }
        var der = DecodificarPem(pem);
        logger.LogInformation("IntegraICP: credentials ok (certificado {Bytes} bytes; campos: {Campos}).",
            der.Length, Campos(doc.RootElement));
        return der;
    }

    public async Task<byte[]> AssinarHashAsync(
        string credencialId, string codeVerifier, byte[] hash, CancellationToken cancellationToken = default)
    {
        GarantirHabilitado();
        // SignaturesRequest da doc v3: verificador em camelCase (secretData/secretType, ao
        // contrário da query string das outras etapas) e o hash DENTRO de requests[].
        // O formato antigo (tudo na raiz, secret_data) dava 400000 "Invalid Request" —
        // conferido em produção em 25/09/2026 contra este formato, que passa da validação.
        var corpo = new Dictionary<string, object>
        {
            ["credentialId"] = credencialId,
            ["secretType"] = "code_verifier",
            ["secretData"] = codeVerifier,
            ["requests"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["contentId"] = "laudo",
                    // Base64 padrão (não URL), como a doc exige.
                    ["contentDigest"] = Convert.ToBase64String(hash),
                    ["contentDescription"] = "Laudo",
                    ["signaturePolicy"] = "RAW",
                },
            },
        };
        using var doc = await EnviarAsync(HttpMethod.Post, $"{Prefixo}/signatures", corpo, "signatures", cancellationToken);

        var assinatura = LerString(doc.RootElement, "signedContent");
        if (!string.IsNullOrWhiteSpace(assinatura))
        {
            var bytes = DecodificarBase64(assinatura);
            logger.LogInformation("IntegraICP: signatures ok (assinatura {Bytes} bytes; campos: {Campos}).",
                bytes.Length, Campos(doc.RootElement));
            return bytes;
        }

        var status = LerString(doc.RootElement, "currentStatus") ?? LerString(doc.RootElement, "status") ?? "(sem status)";
        logger.LogWarning("IntegraICP: /signatures sem signedContent (status {Status}; campos: {Campos}).",
            status, Campos(doc.RootElement));
        throw new ConflitoException("assinatura.nuvem_sem_assinatura",
            $"O provedor não devolveu a assinatura (status {status}). Tente assinar de novo.");
    }

    // ---------------- HTTP ----------------

    private async Task<JsonDocument> EnviarAsync(
        HttpMethod metodo, string caminho, object? corpo, string etapa, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(metodo, caminho);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        if (!string.IsNullOrWhiteSpace(_opt.CabecalhoAutenticacao) && !string.IsNullOrWhiteSpace(_opt.ValorAutenticacao))
            req.Headers.TryAddWithoutValidation(_opt.CabecalhoAutenticacao, _opt.ValorAutenticacao);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "IntegraICP: falha de rede na etapa {Etapa}.", etapa);
            throw new ConflitoException("assinatura.nuvem_indisponivel", "Não foi possível alcançar o serviço de assinatura em nuvem.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "IntegraICP: timeout na etapa {Etapa}.", etapa);
            throw new ConflitoException("assinatura.nuvem_timeout", "O serviço de assinatura em nuvem excedeu o tempo limite.");
        }

        using (resp)
        {
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Corpo de erro pode ecoar o CPF (subject_key): loga só code/message, com
                // qualquer sequência longa de dígitos mascarada.
                logger.LogError("IntegraICP: etapa {Etapa} retornou {Status} ({Bytes} bytes de corpo; erro {Erro}).",
                    etapa, (int)resp.StatusCode, texto.Length, ResumoErro(texto));
                throw new ConflitoException("assinatura.nuvem_erro",
                    $"O serviço de assinatura em nuvem recusou a etapa {etapa} ({(int)resp.StatusCode}).");
            }

            try
            {
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "{}" : texto);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "IntegraICP: resposta não-JSON na etapa {Etapa}.", etapa);
                throw new ConflitoException("assinatura.nuvem_erro", "Resposta inválida do serviço de assinatura em nuvem.");
            }
        }
    }

    private void GarantirHabilitado()
    {
        if (!_opt.Habilitado)
            throw new ConflitoException("assinatura.nuvem_desligada",
                "A assinatura em nuvem não está configurada nesta instância.");
    }

    // ---------------- leitura tolerante ----------------

    /// <summary>
    /// URL de autorização: primeiro valor http(s) encontrado num item da lista de
    /// autorizações (raiz como array, ou a primeira propriedade-array da raiz). Prefere o
    /// item que mencione VIDaaS/Valid quando houver mais de um PSC.
    /// </summary>
    internal static string? AcharUrlAutorizacao(JsonElement raiz)
    {
        // Formato real (25/09/2026): {"data": {"clearances": [...], ...}}.
        if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object)
            raiz = data;

        var itens = new List<JsonElement>();
        if (raiz.ValueKind == JsonValueKind.Array)
            itens.AddRange(raiz.EnumerateArray());
        else if (raiz.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in raiz.EnumerateObject())
            {
                if (p.NameEquals("unavailables")) continue;
                if (p.Value.ValueKind == JsonValueKind.Array) itens.AddRange(p.Value.EnumerateArray());
            }
            if (itens.Count == 0) itens.Add(raiz);
        }

        var ordenados = itens
            .OrderByDescending(i => i.GetRawText().Contains("VIDAAS", StringComparison.OrdinalIgnoreCase)
                                    || i.GetRawText().Contains("VALID", StringComparison.OrdinalIgnoreCase));
        foreach (var item in ordenados)
        {
            var url = PrimeiraUrl(item);
            if (url is not null) return url;
        }
        return null;
    }

    private static string? PrimeiraUrl(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                var s = e.GetString();
                return s is not null && (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                                         || s.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    ? s
                    : null;
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    var u = PrimeiraUrl(p.Value);
                    if (u is not null) return u;
                }
                return null;
            case JsonValueKind.Array:
                foreach (var i in e.EnumerateArray())
                {
                    var u = PrimeiraUrl(i);
                    if (u is not null) return u;
                }
                return null;
            default:
                return null;
        }
    }

    private static string? LerIndisponiveis(JsonElement raiz)
    {
        if (!AcharPropriedade(raiz, "unavailables", out var u)) return null;
        var texto = u.GetRawText();
        return texto.Length > 300 ? texto[..300] : texto;
    }

    /// <summary>
    /// String pelo nome do campo em qualquer nível. A API real embrulha a resposta em
    /// <c>{"data": {...}}</c> (visto em produção em 25/09/2026) — ler só a raiz não acha nada.
    /// </summary>
    internal static string? LerString(JsonElement raiz, string nome) =>
        AcharPropriedade(raiz, nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Busca em largura: o nível mais raso ganha quando o nome se repete.</summary>
    private static bool AcharPropriedade(JsonElement raiz, string nome, out JsonElement valor)
    {
        var fila = new Queue<JsonElement>();
        fila.Enqueue(raiz);
        while (fila.Count > 0)
        {
            var e = fila.Dequeue();
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject())
                {
                    if (string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))
                    {
                        valor = p.Value;
                        return true;
                    }
                }
                foreach (var p in e.EnumerateObject()) fila.Enqueue(p.Value);
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var i in e.EnumerateArray()) fila.Enqueue(i);
            }
        }
        valor = default;
        return false;
    }

    /// <summary>
    /// <c>code</c> + <c>message</c> do corpo de erro (formato real:
    /// <c>{"error":{"code":400101,"message":"Invalid Channel"}}</c>), sem dígitos longos.
    /// </summary>
    internal static string ResumoErro(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var code = AcharPropriedade(doc.RootElement, "code", out var c) ? c.ToString() : "?";
            var msg = LerString(doc.RootElement, "message") ?? "(sem mensagem)";
            if (msg.Length > 200) msg = msg[..200];
            return System.Text.RegularExpressions.Regex.Replace($"{code} {msg}", @"\d{7,}", "***");
        }
        catch (JsonException)
        {
            return "(corpo não-JSON)";
        }
    }

    /// <summary>Caminhos dos campos (sem valores) para diagnóstico em log.</summary>
    private static string Campos(JsonElement raiz)
    {
        var nomes = new List<string>();
        void Visitar(JsonElement e, string prefixo)
        {
            if (nomes.Count >= 40) return;
            if (e.ValueKind == JsonValueKind.Object)
                foreach (var p in e.EnumerateObject())
                {
                    var caminho = prefixo.Length == 0 ? p.Name : $"{prefixo}.{p.Name}";
                    nomes.Add(caminho);
                    Visitar(p.Value, caminho);
                }
            else if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() > 0)
                Visitar(e[0], prefixo + "[0]");
        }
        Visitar(raiz, string.Empty);
        return string.Join(", ", nomes);
    }

    /// <summary>Aceita PEM (com ou sem cabeçalho) ou base64 DER puro.</summary>
    internal static byte[] DecodificarPem(string pem)
    {
        var corpo = new StringBuilder();
        foreach (var linha in pem.Split('\n'))
        {
            var l = linha.Trim();
            if (l.Length == 0 || l.StartsWith("-----", StringComparison.Ordinal)) continue;
            corpo.Append(l);
        }
        return DecodificarBase64(corpo.ToString());
    }

    /// <summary>Base64 padrão ou base64url (a própria API usa base64url no PKCE).</summary>
    internal static byte[] DecodificarBase64(string texto)
    {
        var s = texto.Trim().Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
