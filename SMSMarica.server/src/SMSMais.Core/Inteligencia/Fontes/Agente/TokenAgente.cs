using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.Inteligencia.Fontes.Agente;

/// <summary>
/// Token de conexão do agente proxy. É um bearer secret: geramos aleatório, mostramos ao operador
/// uma única vez e guardamos só o hash (SHA-256). A comparação na conexão é em tempo constante.
/// </summary>
public static class TokenAgente
{
    /// <summary>Gera um token novo (256 bits, base64url) para o operador copiar ao .env do agente.</summary>
    public static string Gerar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Hash hex do token, para guardar em repouso.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Confere o token apresentado contra o hash guardado, em tempo constante.</summary>
    public static bool Confere(string? token, string? hashGuardado)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(hashGuardado))
        {
            return false;
        }

        var apresentado = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        byte[] guardado;
        try
        {
            guardado = Convert.FromHexString(hashGuardado);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(apresentado, guardado);
    }
}
