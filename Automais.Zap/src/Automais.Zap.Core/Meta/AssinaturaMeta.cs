using System.Security.Cryptography;
using System.Text;

namespace Automais.Zap.Core.Meta;

/// <summary>
/// HMAC-SHA256 do corpo cru com o App Secret, no formato <c>sha256=&lt;hex minúsculo&gt;</c> —
/// exatamente o que a Meta manda em <c>X-Hub-Signature-256</c>.
/// </summary>
public static class AssinaturaMeta
{
    public static string Calcular(string appSecret, ReadOnlySpan<byte> corpo)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), corpo);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Comparação em tempo fixo. Devolve false para assinatura ausente ou malformada.</summary>
    public static bool Confere(string appSecret, ReadOnlySpan<byte> corpo, string? assinaturaRecebida)
    {
        if (string.IsNullOrWhiteSpace(assinaturaRecebida)) return false;

        var esperada = Calcular(appSecret, corpo);
        var a = Encoding.UTF8.GetBytes(assinaturaRecebida.Trim());
        var b = Encoding.UTF8.GetBytes(esperada);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
