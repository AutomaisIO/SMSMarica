using System.Security.Cryptography;
using System.Text.Json;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Cidadao.Push;

/// <summary>
/// Conta de serviço do Firebase (o .json baixado em Configurações do projeto → Contas de serviço),
/// guardada inteira no <c>ClientSecret</c> do provedor <c>fcm</c> em Integrações. Só os campos que o
/// servidor usa para pedir o access token e endereçar o envio.
/// </summary>
public sealed record ContaServicoFcm(
    string ProjectId,
    string ClientEmail,
    string PrivateKeyPem,
    string? PrivateKeyId,
    string TokenUri)
{
    public const string TokenUriPadrao = "https://oauth2.googleapis.com/token";

    private const string Codigo = "fcm.conta_servico_invalida";

    /// <summary>
    /// Lê e confere o JSON da conta de serviço. Lança <see cref="ValidacaoException"/> com uma
    /// mensagem que diz o que fazer — é ela que aparece na tela de Integrações ao gravar.
    /// </summary>
    public static ContaServicoFcm Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw Invalida("Cole o JSON da conta de serviço do Firebase (Configurações do projeto → Contas de serviço → Gerar nova chave privada).");

        JsonElement raiz;
        try
        {
            using var doc = JsonDocument.Parse(json);
            raiz = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Invalida("O texto colado não é um JSON válido. Cole o conteúdo inteiro do arquivo .json da conta de serviço.");
        }

        if (raiz.ValueKind != JsonValueKind.Object || Texto(raiz, "type") != "service_account")
            throw Invalida("O JSON não é de uma conta de serviço (o campo \"type\" deveria ser \"service_account\"). Gere a chave em Configurações do projeto → Contas de serviço.");

        var projectId = Texto(raiz, "project_id");
        var clientEmail = Texto(raiz, "client_email");
        var privateKey = Texto(raiz, "private_key");
        var faltando = new[] { ("project_id", projectId), ("client_email", clientEmail), ("private_key", privateKey) }
            .Where(c => string.IsNullOrWhiteSpace(c.Item2))
            .Select(c => c.Item1)
            .ToList();
        if (faltando.Count > 0)
            throw Invalida($"O JSON da conta de serviço está incompleto: falta {string.Join(", ", faltando)}. Cole o arquivo inteiro, sem cortar.");

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            throw Invalida("A chave privada (\"private_key\") do JSON não pôde ser lida. Gere uma chave nova no Firebase e cole o arquivo inteiro.");
        }

        // O access token sai de um POST assinado para este endereço: só aceitamos o do Google, para
        // a credencial não virar um jeito de fazer o servidor chamar um endereço qualquer.
        var tokenUri = Texto(raiz, "token_uri") is { Length: > 0 } t ? t : TokenUriPadrao;
        if (!Uri.TryCreate(tokenUri, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host == "googleapis.com" || uri.Host.EndsWith(".googleapis.com", StringComparison.OrdinalIgnoreCase)))
            throw Invalida("O \"token_uri\" do JSON não é um endereço do Google. Cole o arquivo da conta de serviço sem alterações.");

        return new ContaServicoFcm(projectId!.Trim(), clientEmail!.Trim(), privateKey!, Texto(raiz, "private_key_id"), tokenUri);
    }

    // O ToString gerado do record imprimiria a chave privada em qualquer log que o interpolasse.
    public override string ToString() => $"ContaServicoFcm {{ ProjectId = {ProjectId}, ClientEmail = {ClientEmail} }}";

    private static string? Texto(JsonElement raiz, string campo) =>
        raiz.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static ValidacaoException Invalida(string mensagem) => new(Codigo, mensagem);
}
