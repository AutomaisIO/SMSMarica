using SkiaSharp;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>
/// Reduz uma imagem (JPEG/PNG) para o anexo passar na trava de 5 MB do SER/SERNIT (ticket #167).
/// Reencoda como JPEG e, quando preciso, diminui a resolução — sempre com um <b>piso</b>: não desce
/// abaixo de ~1000 px no lado maior nem de qualidade 45, para não subir pedido/laudo ilegível. Se nem
/// no passo mais econômico couber (ou se o buffer não for imagem decodificável), devolve <c>null</c>
/// e o chamador recusa, como antes.
/// </summary>
public static class CompressorImagem
{
    /// <summary>Piso de resolução: abaixo disto no lado maior um documento digitalizado fica ilegível.</summary>
    public const int LadoMaiorMinimo = 1000;

    /// <summary>Passos (lado maior em px, qualidade JPEG), do mais fiel ao mais econômico.</summary>
    private static readonly (int LadoMaior, int Qualidade)[] Passos =
    [
        (3000, 82), (2400, 75), (2000, 68), (1600, 60), (1280, 52), (LadoMaiorMinimo, 45),
    ];

    /// <summary>
    /// Imagem reencodada em JPEG cabendo em <paramref name="alvoBytes"/>, ou <c>null</c> se nem no
    /// passo mais econômico couber (ou se o buffer não decodificar como imagem).
    /// </summary>
    public static byte[]? ComprimirAte(byte[] original, long alvoBytes)
    {
        using var bitmap = SKBitmap.Decode(original);
        if (bitmap is null) return null;

        foreach (var (ladoMaior, qualidade) in Passos)
        {
            var bytes = Reencodar(bitmap, ladoMaior, qualidade);
            if (bytes is not null && bytes.LongLength <= alvoBytes) return bytes;
        }
        return null;
    }

    /// <summary>
    /// Reencoda a imagem como JPEG no lado maior pedido (sem ampliar) e na qualidade pedida. Usado no
    /// agrupamento de 3+ arquivos, para o PDF único caber em 5 MB. <c>null</c> se não decodificar.
    /// </summary>
    public static byte[]? Reencodar(byte[] original, int ladoMaior, int qualidade)
    {
        using var bitmap = SKBitmap.Decode(original);
        return bitmap is null ? null : Reencodar(bitmap, ladoMaior, qualidade);
    }

    private static byte[]? Reencodar(SKBitmap bitmap, int ladoMaior, int qualidade)
    {
        var maior = Math.Max(bitmap.Width, bitmap.Height);
        SKBitmap? redimensionado = null;
        try
        {
            var alvo = bitmap;
            if (maior > ladoMaior)
            {
                var escala = (double)ladoMaior / maior;
                var largura = Math.Max(1, (int)Math.Round(bitmap.Width * escala));
                var altura = Math.Max(1, (int)Math.Round(bitmap.Height * escala));
                redimensionado = bitmap.Resize(new SKImageInfo(largura, altura), SKFilterQuality.Medium);
                if (redimensionado is not null) alvo = redimensionado;
            }

            using var imagem = SKImage.FromBitmap(alvo);
            using var dados = imagem.Encode(SKEncodedImageFormat.Jpeg, qualidade);
            return dados?.ToArray();
        }
        finally
        {
            redimensionado?.Dispose();
        }
    }
}
