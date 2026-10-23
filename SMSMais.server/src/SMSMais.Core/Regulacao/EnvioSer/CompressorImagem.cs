using SkiaSharp;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>A foto como fica guardada depois da compactação de entrada.</summary>
/// <param name="Endireitada">A foto vinha deitada pela marca de orientação da câmera (EXIF) e foi
/// gravada de pé — vale guardar a versão nova mesmo quando ela quase não diminuiu.</param>
public sealed record ImagemCompactada(byte[] Conteudo, bool Endireitada);

/// <summary>
/// Reduz uma imagem (JPEG/PNG) para o anexo passar na trava de 5 MB do SER/SERNIT (ticket #167).
/// Reencoda como JPEG e, quando preciso, diminui a resolução — sempre com um <b>piso</b>: não desce
/// abaixo de ~1000 px no lado maior nem de qualidade 45, para não subir pedido/laudo ilegível. Se nem
/// no passo mais econômico couber (ou se o buffer não for imagem decodificável), devolve <c>null</c>
/// e o chamador recusa, como antes.
///
/// <para><b>Foto de celular vem deitada.</b> A câmera grava os pixels na posição do sensor e marca no
/// EXIF como girar; o navegador obedece a marca, o SkiaSharp não. Reencodar sem aplicar a marca
/// devolvia um JPEG sem EXIF e de lado — por isso toda decodificação daqui passa por
/// <see cref="DecodificarDePe"/>. Transparência de PNG vira fundo branco (JPEG não tem alfa; sem isso
/// o fundo saía preto).</para>
/// </summary>
public static class CompressorImagem
{
    /// <summary>Piso de resolução: abaixo disto no lado maior um documento digitalizado fica ilegível.</summary>
    public const int LadoMaiorMinimo = 1000;

    /// <summary>
    /// O passo da compactação de entrada (anexo da regulação, 23/10/2026): o segundo da escada, folgado
    /// para letra miúda de exame. Foto de celular de 12 MP (4000×3000, 3–5 MB) vira ~2400×1800 com
    /// algumas centenas de KB.
    /// </summary>
    public static readonly (int LadoMaior, int Qualidade) Entrada = (2400, 75);

    /// <summary>Passos (lado maior em px, qualidade JPEG), do mais fiel ao mais econômico.</summary>
    private static readonly (int LadoMaior, int Qualidade)[] Passos =
    [
        (3000, 82), Entrada, (2000, 68), (1600, 60), (1280, 52), (LadoMaiorMinimo, 45),
    ];

    /// <summary>
    /// Imagem reencodada em JPEG cabendo em <paramref name="alvoBytes"/>, ou <c>null</c> se nem no
    /// passo mais econômico couber (ou se o buffer não decodificar como imagem).
    /// </summary>
    public static byte[]? ComprimirAte(byte[] original, long alvoBytes)
    {
        using var bitmap = DecodificarDePe(original, out _);
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
        using var bitmap = DecodificarDePe(original, out _);
        return bitmap is null ? null : Reencodar(bitmap, ladoMaior, qualidade);
    }

    /// <summary>
    /// A foto no passo de <see cref="Entrada"/>, de pé. <c>null</c> se o buffer não decodificar.
    /// Quem chama decide se vale guardar (só se diminuiu de verdade, ou se foi endireitada).
    /// </summary>
    public static ImagemCompactada? CompactarNaEntrada(byte[] original)
    {
        using var bitmap = DecodificarDePe(original, out var endireitada);
        if (bitmap is null) return null;
        var bytes = Reencodar(bitmap, Entrada.LadoMaior, Entrada.Qualidade);
        return bytes is null ? null : new ImagemCompactada(bytes, endireitada);
    }

    /// <summary>
    /// Decodifica aplicando a orientação do EXIF: o bitmap sai como o navegador mostraria a foto.
    /// <paramref name="endireitada"/> diz se havia giro (ou espelho) a aplicar.
    /// </summary>
    public static SKBitmap? DecodificarDePe(byte[] original, out bool endireitada)
    {
        endireitada = false;
        using var dados = SKData.CreateCopy(original);
        using var codec = SKCodec.Create(dados);
        if (codec is null) return null;

        var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) return null;

        var origem = codec.EncodedOrigin;
        if (origem is SKEncodedOrigin.TopLeft) return bitmap;

        var deLado = origem is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        // Cor de 32 bits sempre: foto em tons de cinza decodifica como Gray8, que não serve de tela.
        var info = deLado
            ? new SKImageInfo(bitmap.Height, bitmap.Width, SKImageInfo.PlatformColorType, AlfaDe(bitmap))
            : new SKImageInfo(bitmap.Width, bitmap.Height, SKImageInfo.PlatformColorType, AlfaDe(bitmap));
        var dePe = new SKBitmap(info);
        using (var canvas = new SKCanvas(dePe))
        {
            canvas.SetMatrix(MatrizDaOrientacao(origem, bitmap.Width, bitmap.Height));
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        bitmap.Dispose();
        endireitada = true;
        return dePe;
    }

    /// <summary>
    /// Leva o pixel (x, y) da foto gravada para onde ele aparece de pé (w × h = tamanho gravado). Os
    /// nomes do enum dizem onde ficam a linha 0 e a coluna 0: <c>RightTop</c> (EXIF 6, o celular em
    /// pé mais comum) é girar 90° no sentido horário.
    /// </summary>
    private static SKMatrix MatrizDaOrientacao(SKEncodedOrigin origem, int w, int h) => origem switch
    {
        // x' = ScaleX·x + SkewX·y + TransX;  y' = SkewY·x + ScaleY·y + TransY
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // espelho horizontal
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 180°
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // espelho vertical
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transposta
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 90° horário
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transversa
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 90° anti-horário
        _ => SKMatrix.Identity,
    };

    /// <summary>Foto sem transparência continua opaca ao girar/redimensionar — senão toda imagem cairia
    /// na composição sobre branco à toa.</summary>
    private static SKAlphaType AlfaDe(SKBitmap bitmap) =>
        bitmap.AlphaType is SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;

    private static byte[]? Reencodar(SKBitmap bitmap, int ladoMaior, int qualidade)
    {
        var maior = Math.Max(bitmap.Width, bitmap.Height);
        SKBitmap? redimensionado = null;
        SKBitmap? semAlfa = null;
        try
        {
            var alvo = bitmap;
            if (maior > ladoMaior)
            {
                var escala = (double)ladoMaior / maior;
                var largura = Math.Max(1, (int)Math.Round(bitmap.Width * escala));
                var altura = Math.Max(1, (int)Math.Round(bitmap.Height * escala));
                redimensionado = bitmap.Resize(
                    new SKImageInfo(largura, altura, SKImageInfo.PlatformColorType, AlfaDe(bitmap)), SKFilterQuality.Medium);
                if (redimensionado is not null) alvo = redimensionado;
            }

            if (alvo.AlphaType is not SKAlphaType.Opaque)
            {
                semAlfa = new SKBitmap(new SKImageInfo(alvo.Width, alvo.Height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque));
                using var canvas = new SKCanvas(semAlfa);
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(alvo, 0, 0);
                alvo = semAlfa;
            }

            using var imagem = SKImage.FromBitmap(alvo);
            using var dados = imagem.Encode(SKEncodedImageFormat.Jpeg, qualidade);
            return dados?.ToArray();
        }
        finally
        {
            redimensionado?.Dispose();
            semAlfa?.Dispose();
        }
    }
}
