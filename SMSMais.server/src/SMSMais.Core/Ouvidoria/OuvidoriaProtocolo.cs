using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Protocolo público (<c>AAAA-NNNNNN</c>) e código de acesso da manifestação (ADR-0060).
/// O código tem 8 caracteres de um alfabeto sem 0/O/1/I (o cidadão dita por telefone); só o
/// SHA-256 fica no banco e a comparação é em tempo constante.
/// </summary>
public static class OuvidoriaProtocolo
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int TamanhoCodigo = 8;

    /// <summary>Gera o código de acesso com <see cref="RandomNumberGenerator"/> (nunca <c>Random</c>).</summary>
    public static string GerarCodigoAcesso()
    {
        var bytes = RandomNumberGenerator.GetBytes(TamanhoCodigo);
        var sb = new StringBuilder(TamanhoCodigo);
        foreach (var b in bytes)
        {
            sb.Append(Alfabeto[b % Alfabeto.Length]);
        }
        return sb.ToString();
    }

    /// <summary>SHA-256 do código normalizado, em hexadecimal minúsculo (64 caracteres).</summary>
    public static string Hash(string codigo)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizarCodigo(codigo))));

    /// <summary>Confere o código contra o hash gravado em tempo constante. Hash nulo (anônima) nunca confere.</summary>
    public static bool Confere(string? codigo, string? hash)
    {
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(hash)) return false;
        var esperado = Encoding.UTF8.GetBytes(hash.Trim().ToLowerInvariant());
        var informado = Encoding.UTF8.GetBytes(Hash(codigo));
        return CryptographicOperations.FixedTimeEquals(esperado, informado);
    }

    public static string Formatar(int ano, long seq) => $"{ano}-{seq:D6}";

    /// <summary>Trim + maiúsculas — o cidadão digita como quiser.</summary>
    public static string Normalizar(string? protocolo) => (protocolo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Código digitado: sem espaços/hífens, maiúsculas.</summary>
    public static string NormalizarCodigo(string? codigo)
        => (codigo ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();
}
