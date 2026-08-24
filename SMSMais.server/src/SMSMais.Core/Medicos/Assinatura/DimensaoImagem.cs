namespace SMSMais.Core.Medicos.Assinatura;

/// <summary>
/// Lê apenas as dimensões (largura/altura em px) de um PNG ou JPEG direto do
/// cabeçalho do arquivo, sem decodificar os pixels nem depender de SkiaSharp.
/// Usado para validar a proporção (1:1 / 2:1) da rubrica no upload.
/// </summary>
public static class DimensaoImagem
{
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Tenta extrair (largura, altura) dos bytes. Retorna <c>null</c> se o
    /// formato não for reconhecido ou o cabeçalho estiver truncado.
    /// </summary>
    public static (int Largura, int Altura)? Ler(byte[] dados)
    {
        if (dados is null || dados.Length < 24) return null;
        return EhPng(dados) ? LerPng(dados) : LerJpeg(dados);
    }

    private static bool EhPng(byte[] d)
    {
        for (var i = 0; i < AssinaturaPng.Length; i++)
        {
            if (d[i] != AssinaturaPng[i]) return false;
        }
        return true;
    }

    // PNG: assinatura (8) + len (4) + "IHDR" (4) + largura (4 BE) + altura (4 BE).
    private static (int, int)? LerPng(byte[] d)
    {
        if (d.Length < 24) return null;
        var largura = LerUInt32BE(d, 16);
        var altura = LerUInt32BE(d, 20);
        return largura > 0 && altura > 0 ? (largura, altura) : null;
    }

    // JPEG: percorre os segmentos até um marcador SOFn, que carrega altura/largura.
    private static (int, int)? LerJpeg(byte[] d)
    {
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return null;

        var pos = 2;
        while (pos + 1 < d.Length)
        {
            if (d[pos] != 0xFF) { pos++; continue; }

            var marcador = d[pos + 1];

            // Marcadores sem payload: padding (0xFF), SOI/EOI/RSTn/TEM.
            if (marcador == 0xFF) { pos++; continue; }
            if (marcador is 0xD8 or 0xD9 or 0x01 || (marcador >= 0xD0 && marcador <= 0xD7))
            {
                pos += 2;
                continue;
            }

            if (pos + 3 >= d.Length) return null;
            var tamanho = (d[pos + 2] << 8) | d[pos + 3];

            // SOF0..SOF15, exceto DHT(C4), JPG(C8) e DAC(CC): altura@+5, largura@+7.
            var ehSof = marcador is >= 0xC0 and <= 0xCF && marcador is not (0xC4 or 0xC8 or 0xCC);
            if (ehSof)
            {
                if (pos + 8 >= d.Length) return null;
                var altura = (d[pos + 5] << 8) | d[pos + 6];
                var largura = (d[pos + 7] << 8) | d[pos + 8];
                return largura > 0 && altura > 0 ? (largura, altura) : null;
            }

            pos += 2 + tamanho;
        }

        return null;
    }

    private static int LerUInt32BE(byte[] d, int offset) =>
        (d[offset] << 24) | (d[offset + 1] << 16) | (d[offset + 2] << 8) | d[offset + 3];
}
