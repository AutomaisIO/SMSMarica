using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.Extensao.Distribuicao;

/// <summary>
/// Segredos da distribuição da extensão (ADR-0064): o código de ativação e o token do computador.
/// Os dois são mostrados uma única vez a quem os recebe; no banco fica só o SHA-256 — a busca é
/// por índice no hash, e um vazamento da tabela não entrega acesso.
/// </summary>
public static class ExtensaoSegredos
{
    // Sem 0/O e 1/I: o código público aparece num endereço e pode ser lido em voz alta.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Segredo de 32 bytes em base64url (código de ativação ou token do computador).</summary>
    public static string GerarSegredo() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Código curto (8 caracteres) que vai no endereço da página de autorização.</summary>
    public static string GerarCodigoPublico()
    {
        Span<char> codigo = stackalloc char[8];
        for (var i = 0; i < codigo.Length; i++)
            codigo[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return new string(codigo);
    }

    /// <summary>Hash SHA-256 (hex minúsculo). Determinístico.</summary>
    public static string Hash(string segredo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(segredo)));
}
