using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.ApiTokens;

/// <summary>
/// Geração e hash de tokens de API. O token em claro segue o formato
/// <c>smk_{base64url}</c> e é mostrado uma única vez; só o hash SHA-256 (hex)
/// é persistido. A verificação refaz o hash e compara — busca O(1) por índice.
/// </summary>
public static class ApiTokenHasher
{
    public const string Prefixo = "smk_";

    /// <summary>Gera um token novo. Retorna o valor em claro e o prefixo legível.</summary>
    public static (string TokenEmClaro, string PrefixoExibicao) Gerar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var corpo = Base64UrlSemPadding(bytes);
        var tokenEmClaro = Prefixo + corpo;
        var prefixoExibicao = Prefixo + corpo[..8]; // ex.: smk_a1B2c3D4 — só para identificar na lista
        return (tokenEmClaro, prefixoExibicao);
    }

    /// <summary>Hash SHA-256 (hex minúsculo) do token. Determinístico.</summary>
    public static string Hash(string tokenEmClaro)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tokenEmClaro));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Base64UrlSemPadding(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
