using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Win32;

// Agente local de assinatura (Opção A — protocolo). NÃO escuta nada, NÃO persiste.
// É lançado pelo navegador via  automais-assinador://assinar?chave=...&server=...
// Fluxo: reivindicar (pela chave) → achar o certificado do CPF → preparar (envia
// cadeia, recebe hash) → assinar o hash (VIDaaS Connect dispara local) → concluir.
//
// Instalação (sem admin):  automais-assinador-agente.exe registrar

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var logPath = Path.Combine(Path.GetTempPath(), "automais-assinador-agente.log");

void Log(string msg)
{
    var linha = $"[{DateTime.Now:HH:mm:ss}] {msg}";
    Console.WriteLine(linha);
    try { File.AppendAllText(logPath, linha + Environment.NewLine); } catch { /* best-effort */ }
}

if (args.Length == 0)
{
    Console.WriteLine("Automais.Assinador.Agente");
    Console.WriteLine("  registrar   → registra o protocolo automais-assinador:// (HKCU, sem admin)");
    Console.WriteLine("  (normalmente é lançado pelo navegador via automais-assinador://...)");
    return 0;
}

if (string.Equals(args[0], "registrar", StringComparison.OrdinalIgnoreCase))
{
    RegistrarProtocolo();
    Log("Protocolo automais-assinador:// registrado para o usuário atual.");
    return 0;
}

if (args[0].StartsWith("automais-assinador://", StringComparison.OrdinalIgnoreCase))
{
    return await AssinarAsync(args[0]);
}

Console.Error.WriteLine($"Argumento não reconhecido: {args[0]}");
return 1;

// ---------------- assinatura ----------------

async Task<int> AssinarAsync(string url)
{
    Uri uri;
    try { uri = new Uri(url); }
    catch { Log($"URL inválida: {url}"); return 2; }

    var q = ParseQuery(uri.Query);
    var chave = q.GetValueOrDefault("chave");
    var server = q.GetValueOrDefault("server");
    if (string.IsNullOrWhiteSpace(chave) || string.IsNullOrWhiteSpace(server))
    {
        Log("URL sem 'chave' ou 'server'.");
        return 2;
    }

    Log($"Assinatura solicitada (server={server}).");
    using var http = new HttpClient { BaseAddress = new Uri(server), Timeout = TimeSpan.FromSeconds(90) };

    try
    {
        // 1) Reivindica o job pela chave → recebe o CPF do médico (para achar o certificado).
        var reiv = await PostAsync<ReivindicarResp>(http, "assinatura/agente/reivindicar", new { chave }, jsonOpts);
        var cpf = reiv.CpfMedico ?? string.Empty;
        Log($"Job: laudo \"{reiv.LaudoTitulo}\" — CPF {cpf}.");

        // 2) Acha o certificado na loja do Windows.
        using var cert = LocalizarCertificado(cpf);
        if (cert is null)
        {
            Log($"Certificado do CPF {cpf} não encontrado na loja do Windows.");
            return 3;
        }
        var cadeia = MontarCadeia(cert);

        // 3) Envia a cadeia, recebe o hash a assinar.
        var prep = await PostAsync<PrepararResp>(http, "assinatura/agente/preparar",
            new { chave, cadeiaCertificadoBase64 = cadeia }, jsonOpts);
        var hash = Convert.FromBase64String(prep.ToSignHashBase64);

        // 4) Assina o hash — a chave é do provedor VIDaaS Connect, então o prompt local dispara.
        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificado sem chave privada RSA acessível.");
        var assinatura = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // 5) Devolve a assinatura crua; o servidor embute o CMS e conclui.
        await PostVoidAsync(http, "assinatura/agente/concluir",
            new { chave, rawSignatureBase64 = Convert.ToBase64String(assinatura) }, jsonOpts);

        Log("Assinatura concluída com sucesso.");
        return 0;
    }
    catch (Exception ex)
    {
        Log($"Falha: {ex.Message}");
        return 4;
    }
}

static async Task<T> PostAsync<T>(HttpClient http, string caminho, object corpo, JsonSerializerOptions opts)
{
    using var resp = await http.PostAsJsonAsync(caminho, corpo, opts);
    if (!resp.IsSuccessStatusCode)
        throw new InvalidOperationException($"{caminho} → {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
    return (await resp.Content.ReadFromJsonAsync<T>(opts))!;
}

static async Task PostVoidAsync(HttpClient http, string caminho, object corpo, JsonSerializerOptions opts)
{
    using var resp = await http.PostAsJsonAsync(caminho, corpo, opts);
    if (!resp.IsSuccessStatusCode)
        throw new InvalidOperationException($"{caminho} → {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
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

static X509Certificate2? LocalizarCertificado(string cpf)
{
    var digitos = new string([.. cpf.Where(char.IsDigit)]);
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    foreach (var c in store.Certificates)
    {
        if (c.HasPrivateKey && digitos.Length == 11 && c.Subject.Contains(digitos, StringComparison.Ordinal))
            return c;
    }
    return null;
}

static List<string> MontarCadeia(X509Certificate2 cert)
{
    var cadeia = new List<string> { Convert.ToBase64String(cert.RawData) };
    using var chain = new X509Chain();
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    chain.Build(cert);
    foreach (var elemento in chain.ChainElements)
    {
        if (elemento.Certificate.Thumbprint != cert.Thumbprint)
            cadeia.Add(Convert.ToBase64String(elemento.Certificate.RawData));
    }
    return cadeia;
}

static void RegistrarProtocolo()
{
    var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável não resolvido.");
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
