using System.Security.Cryptography;
using System.Text.Json;

namespace SMSMais.Tests.Cidadao.Push;

/// <summary>JSON de conta de serviço com uma chave RSA gerada na hora — o formato que o Firebase baixa.</summary>
internal static class ContaServicoFcmFabrica
{
    public const string ProjectId = "projeto-teste";
    public const string ClientEmail = "push@projeto-teste.iam.gserviceaccount.com";
    public const string PrivateKeyId = "kid-123";

    public static (string Json, RSA Chave) Gerar(
        string? tipo = "service_account",
        string? tokenUri = "https://oauth2.googleapis.com/token",
        bool semChave = false)
    {
        var rsa = RSA.Create(2048);
        var campos = new Dictionary<string, object?>
        {
            ["type"] = tipo,
            ["project_id"] = ProjectId,
            ["private_key_id"] = PrivateKeyId,
            ["private_key"] = semChave ? null : rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = ClientEmail,
            ["client_id"] = "1234567890",
            ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
            ["token_uri"] = tokenUri,
        };
        return (JsonSerializer.Serialize(campos), rsa);
    }
}
