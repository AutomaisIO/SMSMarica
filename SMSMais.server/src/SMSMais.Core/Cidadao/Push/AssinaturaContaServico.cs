using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SMSMais.Core.Cidadao.Push;

/// <summary>
/// Asserção JWT (RS256) da conta de serviço, trocada no <c>token_uri</c> do Google por um access
/// token OAuth2 (fluxo <c>jwt-bearer</c>). Feita à mão para não trazer pacote do Google: a API
/// <c>GoogleCredential.FromJson</c> foi marcada obsoleta e o FirebaseAdmin tem estado global e
/// escopos amplos — aqui o escopo é só o de mandar mensagem.
/// </summary>
internal static class AssinaturaContaServico
{
    public const string EscopoFcm = "https://www.googleapis.com/auth/firebase.messaging";

    /// <summary>O Google aceita no máximo 1 hora entre <c>iat</c> e <c>exp</c>.</summary>
    private const int ValidadeSegundos = 3600;

    public static string MontarAssercao(ContaServicoFcm conta, DateTimeOffset agora)
    {
        var cabecalho = new Dictionary<string, string> { ["alg"] = "RS256", ["typ"] = "JWT" };
        if (!string.IsNullOrWhiteSpace(conta.PrivateKeyId)) cabecalho["kid"] = conta.PrivateKeyId;

        var iat = agora.ToUnixTimeSeconds();
        var declaracoes = new Dictionary<string, object>
        {
            ["iss"] = conta.ClientEmail,
            ["scope"] = EscopoFcm,
            ["aud"] = conta.TokenUri,
            ["iat"] = iat,
            ["exp"] = iat + ValidadeSegundos,
        };

        var conteudo = $"{Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(cabecalho))}"
            + $".{Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(declaracoes))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(conta.PrivateKeyPem);
        var assinatura = rsa.SignData(
            Encoding.ASCII.GetBytes(conteudo), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{conteudo}.{Base64Url.EncodeToString(assinatura)}";
    }
}
