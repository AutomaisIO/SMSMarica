using System.Security.Cryptography;
using System.Text;

namespace Automais.Zap.Core.Entregas;

/// <summary>
/// Assinatura do relay para a aplicação de destino. É contrato ENTRE OS NOSSOS SISTEMAS, e por
/// isso não imita o esquema da Meta: pedir emprestado o App Secret dela para autenticar uma
/// conversa nossa era acoplamento sem ganho — e quebrou no primeiro dia em que existiram dois
/// Apps com segredos diferentes.
///
/// <para>Formato: <c>X-Automais-Signature: sha256=&lt;hex&gt;</c> sobre
/// <c>{timestamp}.{corpo}</c>, com o instante em <c>X-Automais-Timestamp</c> (epoch em
/// segundos). O timestamp entra no que é assinado para que capturar uma entrega não permita
/// reenviá-la depois — HMAC só do corpo seria replayável.</para>
/// </summary>
public static class AssinaturaAutomais
{
    public const string CabecalhoAssinatura = "X-Automais-Signature";
    public const string CabecalhoTimestamp = "X-Automais-Timestamp";

    /// <summary>Tolerância de relógio aceita pelo destino.</summary>
    public static readonly TimeSpan JanelaAceita = TimeSpan.FromMinutes(5);

    public static string Calcular(string segredo, long timestamp, ReadOnlySpan<byte> corpo)
    {
        var prefixo = Encoding.UTF8.GetBytes($"{timestamp}.");
        var conteudo = new byte[prefixo.Length + corpo.Length];
        prefixo.CopyTo(conteudo, 0);
        corpo.CopyTo(conteudo.AsSpan(prefixo.Length));

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), conteudo);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Confere(string segredo, long timestamp, ReadOnlySpan<byte> corpo, string? recebida)
    {
        if (string.IsNullOrWhiteSpace(recebida)) return false;

        var esperada = Calcular(segredo, timestamp, corpo);
        var a = Encoding.UTF8.GetBytes(recebida.Trim());
        var b = Encoding.UTF8.GetBytes(esperada);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Gera um segredo novo para compartilhar com a aplicação de destino.</summary>
    public static string GerarSegredo() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
