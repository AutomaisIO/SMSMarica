using System.Buffers;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// Leitura de stream com teto rígido de memória. Usado para bufferizar respostas
/// do PACS antes de cachear SEM depender de <c>Content-Length</c> — respostas
/// WADO-RS multipart do dcm4chee costumam vir em <c>chunked</c> (sem tamanho
/// declarado), então confiar no header deixaria a leitura sem limite.
/// </summary>
public static class StreamLimitado
{
    /// <summary>
    /// Teto de segurança quando o cache não tem teto por item configurado
    /// (<c>TamanhoMaximoItemMb &lt;= 0</c>). Garante que a memória seja sempre
    /// limitada, mesmo em configuração permissiva.
    /// </summary>
    public const long TetoSegurancaPadrao = 256L * 1024 * 1024;

    /// <summary>
    /// Lê <paramref name="origem"/> acumulando bytes até <paramref name="teto"/>.
    /// Se o stream terminar dentro do teto, devolve <c>Completo = true</c> e todos
    /// os bytes (a resposta inteira, cacheável). Se ultrapassar, devolve
    /// <c>Completo = false</c> e apenas o prefixo já lido — o restante continua no
    /// <paramref name="origem"/> e deve ser repassado por streaming, sem cachear.
    /// </summary>
    public static async Task<(byte[] Buffer, bool Completo)> LerComTetoAsync(
        Stream origem, long teto, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int lidos;
            while ((lidos = await origem.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                ms.Write(buffer, 0, lidos);
                if (teto > 0 && ms.Length > teto)
                {
                    // Ultrapassou: devolve o prefixo lido; o resto fica em `origem`.
                    return (ms.ToArray(), false);
                }
            }
            return (ms.ToArray(), true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
