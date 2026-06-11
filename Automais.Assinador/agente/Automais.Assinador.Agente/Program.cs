using System.Formats.Asn1;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

// Agente local de assinatura (Opção A — protocolo). NÃO escuta nada, NÃO persiste.
// Lançado pelo navegador via  automais-assinador://assinar?chave=...&server=...
// Fluxo: reivindicar (pela chave) -> achar o cert do CPF -> preparar (envia cadeia,
// recebe hash) -> assinar o hash (VIDaaS Connect dispara local) -> concluir.
//
// Instalação (sem admin):  automais-assinador-agente.exe registrar

// Hosts confiáveis do backend. O 'server' da URL é validado contra esta lista —
// impede que uma página hostil aponte o agente para um servidor de atacante.
string[] hostsPermitidos = ["api.smsmarica.online", "localhost", "127.0.0.1"];

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var logPath = Path.Combine(Path.GetTempPath(), "automais-assinador-agente.log");

void Log(string msg)
{
    var linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}";
    Console.WriteLine(linha);
    try { File.AppendAllText(logPath, linha + Environment.NewLine); } catch { /* best-effort */ }
}

if (args.Length == 0)
{
    Console.WriteLine("Automais.Assinador.Agente");
    Console.WriteLine("  registrar   -> registra o protocolo automais-assinador:// (HKCU, sem admin)");
    Console.WriteLine("  (normalmente e lancado pelo navegador via automais-assinador://...)");
    return 0;
}

if (string.Equals(args[0], "registrar", StringComparison.OrdinalIgnoreCase))
{
    RegistrarProtocolo();
    Log("Protocolo automais-assinador:// registrado para o usuario atual.");
    return 0;
}

if (args[0].StartsWith("automais-assinador://", StringComparison.OrdinalIgnoreCase))
{
    return await AssinarAsync(args[0]);
}

Console.Error.WriteLine($"Argumento nao reconhecido: {args[0]}");
return 1;

// ---------------- assinatura ----------------

async Task<int> AssinarAsync(string url)
{
    Log("==== nova solicitacao de assinatura ====");
    Uri uri;
    try { uri = new Uri(url); }
    catch { Log($"URL invalida: {url}"); return 2; }

    var q = ParseQuery(uri.Query);
    var chave = q.GetValueOrDefault("chave");
    var server = q.GetValueOrDefault("server");
    if (string.IsNullOrWhiteSpace(chave) || string.IsNullOrWhiteSpace(server))
    {
        Log("URL sem 'chave' ou 'server'.");
        return 2;
    }

    // SEGURANCA: so falamos com hosts oficiais, e em https (exceto loopback de dev).
    if (!Uri.TryCreate(server, UriKind.Absolute, out var su) || !ServidorPermitido(su))
    {
        Log($"Servidor NAO permitido (allowlist {string.Join(",", hostsPermitidos)}): {server}");
        return 2;
    }

    Log($"Servidor: {su}");
    using var http = new HttpClient { BaseAddress = su, Timeout = TimeSpan.FromSeconds(90) };

    try
    {
        // 1) Reivindica o job pela chave -> recebe o CPF do medico.
        var reiv = await PostAsync<ReivindicarResp>(http, "assinatura/agente/reivindicar", new { chave }, jsonOpts);
        var cpf = new string([.. (reiv.CpfMedico ?? "").Where(char.IsDigit)]);
        Log($"Job reivindicado: laudo \"{reiv.LaudoTitulo}\" — CPF alvo {cpf}.");

        // 2) Acha o certificado na loja do Windows (por CPF canonico ICP-Brasil).
        Log("Varrendo certificados (CurrentUser\\My) — DIAGNOSTICO:");
        using var cert = LocalizarCertificado(cpf, Log);
        if (cert is null)
        {
            Log($"Nenhum certificado com chave privada e CPF {cpf} (valido) encontrado.");
            return 3;
        }
        Log($"Certificado escolhido: subject='{cert.Subject}' thumb={cert.Thumbprint} validade={cert.NotBefore:dd/MM/yyyy}-{cert.NotAfter:dd/MM/yyyy}");

        var cadeia = MontarCadeia(cert, Log);

        // 3) Envia a cadeia, recebe o hash a assinar.
        var prep = await PostAsync<PrepararResp>(http, "assinatura/agente/preparar",
            new { chave, cadeiaCertificadoBase64 = cadeia }, jsonOpts);
        var hash = Convert.FromBase64String(prep.ToSignHashBase64);
        Log($"Hash recebido ({hash.Length} bytes, {prep.AlgoritmoHash}); assinando...");

        // 4) Assina o hash — a chave e do provedor VIDaaS Connect, dispara o prompt local.
        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificado sem chave privada RSA acessivel.");
        var assinatura = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Log($"Hash assinado ({assinatura.Length} bytes). Concluindo no servidor...");

        // 5) Devolve a assinatura crua; o servidor embute o CMS e conclui.
        await PostVoidAsync(http, "assinatura/agente/concluir",
            new { chave, rawSignatureBase64 = Convert.ToBase64String(assinatura) }, jsonOpts);

        Log("OK — assinatura concluida com sucesso.");
        return 0;
    }
    catch (FormatException ex) { Log($"Falha: payload base64 invalido do servidor ({ex.Message})."); return 5; }
    catch (TaskCanceledException) { Log("Falha: timeout (90s) ao falar com o servidor."); return 6; }
    catch (HttpRequestException ex) { Log($"Falha de rede: {ex.Message}"); return 7; }
    catch (Exception ex) { Log($"Falha ({ex.GetType().Name}): {ex.Message}"); return 4; }
}

bool ServidorPermitido(Uri uri)
{
    var ehLoopback = uri.Host is "localhost" or "127.0.0.1";
    if (!ehLoopback && uri.Scheme != Uri.UriSchemeHttps) return false;
    return hostsPermitidos.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
}

static async Task<T> PostAsync<T>(HttpClient http, string caminho, object corpo, JsonSerializerOptions opts)
{
    using var resp = await http.PostAsJsonAsync(caminho, corpo, opts);
    if (!resp.IsSuccessStatusCode)
        throw new InvalidOperationException($"{caminho} -> {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
    return (await resp.Content.ReadFromJsonAsync<T>(opts))!;
}

static async Task PostVoidAsync(HttpClient http, string caminho, object corpo, JsonSerializerOptions opts)
{
    using var resp = await http.PostAsJsonAsync(caminho, corpo, opts);
    if (!resp.IsSuccessStatusCode)
        throw new InvalidOperationException($"{caminho} -> {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
}

static Dictionary<string, string> ParseQuery(string query)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var parte in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = parte.Split('=', 2);
        dict[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
    }
    return dict;
}

// Seleciona o cert com chave privada cujo CPF ICP-Brasil == alvo e esteja válido.
// Loga TODOS os candidatos (diagnóstico) para análise pós-teste.
static X509Certificate2? LocalizarCertificado(string cpfAlvo, Action<string> log)
{
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    X509Certificate2? escolhido = null;
    foreach (var c in store.Certificates)
    {
        if (!c.HasPrivateKey) continue;
        var cpfCert = CpfDoCertificado(c);
        var valido = DateTime.Now >= c.NotBefore && DateTime.Now <= c.NotAfter;
        log($"  - subject='{c.Subject}' cpfCert='{cpfCert ?? "(nao extraido)"}' validade={(valido ? "OK" : "VENCIDO")} thumb={c.Thumbprint}");
        if (escolhido is null && valido && cpfCert == cpfAlvo)
            escolhido = c;
    }
    return escolhido;
}

// CPF ICP-Brasil: primario = OtherName OID 2.16.76.1.3.1 do SubjectAltName (DOC-ICP-04),
// fallback = CN no formato "NOME:CPF".
static string? CpfDoCertificado(X509Certificate2 cert)
{
    var san = cert.Extensions.FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");
    if (san is not null)
    {
        var doSan = CpfDoSan(san.RawData);
        if (doSan is not null) return doSan;
    }
    var cn = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false) ?? string.Empty;
    var idx = cn.LastIndexOf(':');
    if (idx >= 0 && idx < cn.Length - 1)
    {
        var suf = new string([.. cn[(idx + 1)..].Where(char.IsDigit)]);
        if (suf.Length >= 11) return suf[..11];
    }
    return null;
}

static string? CpfDoSan(byte[] sanRaw)
{
    try
    {
        var outer = new AsnReader(sanRaw, AsnEncodingRules.DER).ReadSequence();
        while (outer.HasData)
        {
            var tag = outer.PeekTag();
            if (tag is { TagClass: TagClass.ContextSpecific, TagValue: 0 })
            {
                var other = outer.ReadSequence(tag);
                var oid = other.ReadObjectIdentifier();
                if (oid == "2.16.76.1.3.1")
                {
                    var valueExplicit = other.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
                    var texto = LerTexto(valueExplicit);
                    var digitos = new string([.. (texto ?? string.Empty).Where(char.IsDigit)]);
                    // DOC-ICP-04 pessoa fisica: nascimento(8) + CPF(11) + ...
                    if (digitos.Length >= 19) return digitos.Substring(8, 11);
                    if (digitos.Length == 11) return digitos;
                    return null;
                }
            }
            else
            {
                outer.ReadEncodedValue();
            }
        }
    }
    catch { /* SAN malformado: sem CPF por aqui */ }
    return null;
}

static string? LerTexto(AsnReader r)
{
    try
    {
        var tag = r.PeekTag();
        if (tag.TagClass == TagClass.Universal)
        {
            var u = (UniversalTagNumber)tag.TagValue;
            if (u == UniversalTagNumber.OctetString)
                return Encoding.ASCII.GetString(r.ReadOctetString());
            if (u is UniversalTagNumber.UTF8String or UniversalTagNumber.PrintableString
                or UniversalTagNumber.IA5String or UniversalTagNumber.T61String)
                return r.ReadCharacterString(u);
        }
        return Encoding.ASCII.GetString(r.ReadEncodedValue().ToArray());
    }
    catch { return null; }
}

static List<string> MontarCadeia(X509Certificate2 cert, Action<string> log)
{
    var cadeia = new List<string> { Convert.ToBase64String(cert.RawData) };
    using var chain = new X509Chain();
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    var ok = chain.Build(cert);
    if (!ok)
        log($"  AVISO: cadeia incompleta/nao confiavel ({string.Join("; ", chain.ChainStatus.Select(s => s.StatusInformation?.Trim()))}). Faltam ACs intermediarias ICP-Brasil?");
    foreach (var elemento in chain.ChainElements)
    {
        if (elemento.Certificate.Thumbprint != cert.Thumbprint)
            cadeia.Add(Convert.ToBase64String(elemento.Certificate.RawData));
    }
    log($"  cadeia montada com {cadeia.Count} certificado(s).");
    return cadeia;
}

static void RegistrarProtocolo()
{
    var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executavel nao resolvido.");
    using (var raiz = Registry.CurrentUser.CreateSubKey(@"Software\Classes\automais-assinador"))
    {
        raiz.SetValue(string.Empty, "URL:Automais Assinador");
        raiz.SetValue("URL Protocol", string.Empty);
    }
    using (var icone = Registry.CurrentUser.CreateSubKey(@"Software\Classes\automais-assinador\DefaultIcon"))
    {
        icone.SetValue(string.Empty, $"\"{exe}\",0");
    }
    using var cmd = Registry.CurrentUser.CreateSubKey(@"Software\Classes\automais-assinador\shell\open\command");
    cmd.SetValue(string.Empty, $"\"{exe}\" \"%1\"");
}

internal sealed record ReivindicarResp(Guid AssinaturaId, string? CpfMedico, string LaudoTitulo);

internal sealed record PrepararResp(string ToSignHashBase64, string AlgoritmoHash);
