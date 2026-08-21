using System.Security.Cryptography;

namespace Automais.Zap.Core.Admin;

/// <summary>
/// PBKDF2-SHA256. Formato guardado: <c>iteracoes.salt_b64.hash_b64</c> — as iterações vão
/// junto para que subir o custo depois não invalide as senhas já existentes.
/// </summary>
public static class Senhas
{
    private const int IteracoesPadrao = 210_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;

    public static string Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, IteracoesPadrao, HashAlgorithmName.SHA256, TamanhoHash);
        return $"{IteracoesPadrao}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Confere(string senha, string armazenado)
    {
        var partes = armazenado.Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iteracoes) || iteracoes <= 0) return false;

        byte[] salt, esperado;
        try
        {
            salt = Convert.FromBase64String(partes[1]);
            esperado = Convert.FromBase64String(partes[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
