using System.Globalization;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>Como terminou a tentativa de comprimir um PDF para caber no limite do SER/SERNIT.</summary>
public enum DesfechoCompressaoPdf
{
    /// <summary>Coube: <see cref="CompressaoPdf.Conteudo"/> é o PDF menor.</summary>
    Coube,

    /// <summary>Nem no degrau mais econômico da escada coube.</summary>
    NaoCoube,

    /// <summary>Não há Ghostscript neste servidor (ou o caminho configurado não existe).</summary>
    Indisponivel,

    /// <summary>O Ghostscript recusou o arquivo, estourou o tempo ou devolveu algo que não é PDF.</summary>
    Falhou,
}

/// <param name="Degrau">Rótulo do degrau que coube ("200 dpi") — a prévia mostra ao regulador.</param>
public sealed record CompressaoPdf(DesfechoCompressaoPdf Desfecho, byte[]? Conteudo = null, string? Degrau = null)
{
    public static readonly CompressaoPdf Indisponivel = new(DesfechoCompressaoPdf.Indisponivel);
    public static readonly CompressaoPdf Falhou = new(DesfechoCompressaoPdf.Falhou);
    public static readonly CompressaoPdf NaoCoube = new(DesfechoCompressaoPdf.NaoCoube);
}

/// <summary>
/// Comprime um PDF — na prática, páginas escaneadas: cada página é uma foto JPEG — até caber num
/// tamanho. Reduz a resolução e a qualidade das imagens de dentro dele; texto e layout ficam.
/// </summary>
public interface ICompressorPdf
{
    Task<CompressaoPdf> ComprimirAteAsync(byte[] pdf, long alvoBytes, CancellationToken ct);

    /// <summary>
    /// Uma passada só, no degrau dado — a compactação de entrada (<see cref="EscadaCompressaoPdf.Entrada"/>),
    /// que não persegue um limite: só deixa o arquivo enxuto antes de guardar. <c>null</c> = não deu
    /// (Ghostscript fora, arquivo recusado, prazo estourado); o motivo fica no log.
    /// </summary>
    Task<byte[]?> ComprimirNoDegrauAsync(byte[] pdf, DegrauPdf degrau, CancellationToken ct);
}

/// <summary>Um degrau da escada: resolução máxima das imagens e qualidade JPEG (QFactor do Ghostscript —
/// quanto MAIOR, mais comprime: 0,4 ≈ alta, 0,76 ≈ média, 1,3 ≈ baixa).</summary>
public sealed record DegrauPdf(int Dpi, double QFactor)
{
    public string Rotulo => $"{Dpi} dpi";

    public string QFactorTexto => QFactor.ToString("0.0#", CultureInfo.InvariantCulture);
}

/// <summary>
/// A escada de compressão de PDF, do mais fiel ao mais econômico, e a busca do degrau mais fiel que
/// cabe. Medida em 21/10/2026 num pedido real do SERNIT (10,4 MB, 4 páginas escaneadas): 300 dpi/0,4
/// deu 6,65 MB; 200/0,4 = 3,19 MB; 200/0,76 = 2,32 MB; 170/0,76 = 1,72 MB; 150/0,76 = 1,33 MB;
/// 150/1,3 = 1,00 MB — todos legíveis. O piso é 150 dpi: abaixo disso letra pequena de documento
/// escaneado começa a borrar.
/// </summary>
public static class EscadaCompressaoPdf
{
    public static readonly IReadOnlyList<DegrauPdf> Degraus =
    [
        new(300, 0.4), new(200, 0.4), new(200, 0.76), new(170, 0.76), new(150, 0.76), new(150, 1.3),
    ];

    public static DegrauPdf Piso => Degraus[^1];

    /// <summary>
    /// O degrau da compactação de entrada (anexo da regulação, 23/10/2026): 200 dpi, qualidade média —
    /// no pedido medido acima, 10,4 MB viraram 2,32 MB legíveis. Folgado de propósito: a pessoa confere
    /// a prévia, mas o que se guarda é só esta cópia.
    /// </summary>
    public static DegrauPdf Entrada => Degraus[2];

    /// <summary>
    /// Busca binária do degrau <b>mais fiel</b> cujo resultado cabe em <paramref name="alvoBytes"/> —
    /// o tamanho cai a cada degrau, então bastam até 3 passadas para 6 degraus, em vez de descer um a um
    /// (cada passada do Ghostscript leva segundos, e a prévia espera por ela).
    /// </summary>
    /// <param name="passada">Comprime no degrau de índice dado; <c>null</c> = falhou (e a busca para).</param>
    public static async Task<ResultadoEscada> BuscarAsync(
        int quantidade, Func<int, CancellationToken, Task<byte[]?>> passada, long alvoBytes, CancellationToken ct)
    {
        byte[]? melhor = null;
        var indiceMelhor = -1;
        var passadas = 0;
        int baixo = 0, alto = quantidade - 1;
        while (baixo <= alto)
        {
            var meio = (baixo + alto) / 2;
            var resultado = await passada(meio, ct);
            passadas++;
            if (resultado is null) return new ResultadoEscada(DesfechoCompressaoPdf.Falhou, null, -1, passadas);

            if (resultado.LongLength <= alvoBytes)
            {
                melhor = resultado;
                indiceMelhor = meio;
                alto = meio - 1;
            }
            else
            {
                baixo = meio + 1;
            }
        }

        return melhor is null
            ? new ResultadoEscada(DesfechoCompressaoPdf.NaoCoube, null, -1, passadas)
            : new ResultadoEscada(DesfechoCompressaoPdf.Coube, melhor, indiceMelhor, passadas);
    }
}

/// <param name="Indice">O degrau que coube (em <see cref="EscadaCompressaoPdf.Degraus"/>), ou -1.</param>
public sealed record ResultadoEscada(DesfechoCompressaoPdf Desfecho, byte[]? Conteudo, int Indice, int Passadas);
