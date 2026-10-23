using Microsoft.Extensions.Logging.Abstractions;

using SkiaSharp;

using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.EnvioSer;

namespace SMSMais.Tests.Regulacao.Anexos;

/// <summary>
/// Compactação do anexo da regulação na entrada (23/10/2026): o que se guarda é a cópia enxuta, então
/// ela tem de sair legível e de pé — e o que não compensa compactar vai como chegou.
/// </summary>
public class CompactadorAnexoTests
{
    private sealed class CompressorPdfFalso(byte[]? resposta) : ICompressorPdf
    {
        public DegrauPdf? Degrau { get; private set; }

        public Task<CompressaoPdf> ComprimirAteAsync(byte[] pdf, long alvoBytes, CancellationToken ct) =>
            throw new NotSupportedException("A entrada não persegue limite.");

        public Task<byte[]?> ComprimirNoDegrauAsync(byte[] pdf, DegrauPdf degrau, CancellationToken ct)
        {
            Degrau = degrau;
            return Task.FromResult(resposta);
        }
    }

    private static CompactadorAnexo Compactador(byte[]? respostaPdf = null) =>
        new(new CompressorPdfFalso(respostaPdf), NullLogger<CompactadorAnexo>.Instance);

    /// <summary>Foto "de celular": grande, com ruído (não comprime fácil), em qualidade alta.</summary>
    private static byte[] FotoGrande(int largura = 3200, int altura = 2400)
    {
        using var bitmap = ComRuido(largura, altura, 42);
        return Codificar(bitmap, SKEncodedImageFormat.Jpeg, 95);
    }

    /// <summary>Pixels de uma vez (SetPixel um a um levaria segundos nesse tamanho).</summary>
    private static SKBitmap ComRuido(int largura, int altura, int semente)
    {
        var aleatorio = new Random(semente);
        var pixels = new SKColor[largura * altura];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = new SKColor((byte)aleatorio.Next(256), (byte)aleatorio.Next(256), (byte)(i % 256));
        return new SKBitmap(largura, altura) { Pixels = pixels };
    }

    private static byte[] Codificar(SKBitmap bitmap, SKEncodedImageFormat formato, int qualidade)
    {
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(formato, qualidade);
        return dados.ToArray();
    }

    /// <summary>
    /// Põe no JPEG a marca de orientação do EXIF (APP1 logo depois do SOI), como a câmera do celular
    /// faz: os pixels ficam como o sensor viu e a marca diz como girar.
    /// </summary>
    private static byte[] ComOrientacaoExif(byte[] jpeg, ushort orientacao)
    {
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian, IFD0 no byte 8
            0x00, 0x01,                                                 // uma entrada
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,             // Orientation, SHORT, 1 valor
            (byte)(orientacao >> 8), (byte)orientacao, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,                                     // sem próximo IFD
        ];
        byte[] exif = [.. "Exif\0\0"u8.ToArray(), .. tiff];
        var tamanho = exif.Length + 2;
        byte[] app1 = [0xFF, 0xE1, (byte)(tamanho >> 8), (byte)tamanho, .. exif];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    private static SKBitmap Decodificar(byte[] bytes) => SKBitmap.Decode(bytes);

    [Fact]
    public async Task Foto_de_celular_vira_jpeg_menor_dentro_do_lado_de_entrada()
    {
        var original = FotoGrande();

        var r = await Compactador().CompactarAsync("IMG_0001.jpg", "image/jpeg", original, CancellationToken.None);

        r.TamanhoOriginal.Should().Be(original.LongLength);
        r.Conteudo.LongLength.Should().BeLessThan(original.LongLength / 2);
        r.ContentType.Should().Be("image/jpeg");
        using var guardada = Decodificar(r.Conteudo);
        Math.Max(guardada.Width, guardada.Height).Should().Be(CompressorImagem.Entrada.LadoMaior);
    }

    [Fact]
    public async Task Png_vira_jpg_no_nome_e_no_tipo()
    {
        using var bitmap = ComRuido(2800, 2000, 7);
        var png = Codificar(bitmap, SKEncodedImageFormat.Png, 100);

        var r = await Compactador().CompactarAsync("print da tela.png", "image/png", png, CancellationToken.None);

        r.Nome.Should().Be("print da tela.jpg");
        r.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Foto_ja_enxuta_vai_como_chegou()
    {
        using var bitmap = new SKBitmap(800, 600);
        bitmap.Erase(new SKColor(200, 200, 200));
        var pequena = Codificar(bitmap, SKEncodedImageFormat.Jpeg, 50);

        var r = await Compactador().CompactarAsync("foto.jpg", "image/jpeg", pequena, CancellationToken.None);

        r.Conteudo.Should().BeSameAs(pequena, "recomprimir sem ganho só perde qualidade");
        r.Nome.Should().Be("foto.jpg");
    }

    [Fact]
    public async Task Foto_deitada_pelo_exif_e_guardada_de_pe()
    {
        // 60×20 gravada pelo sensor: metade esquerda vermelha, direita azul. EXIF 6 = girar 90° no
        // sentido horário — de pé, fica 20×60 com o vermelho em cima.
        using var bitmap = new SKBitmap(60, 20);
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 60; x++)
                bitmap.SetPixel(x, y, x < 30 ? SKColors.Red : SKColors.Blue);
        }
        var deitada = ComOrientacaoExif(Codificar(bitmap, SKEncodedImageFormat.Jpeg, 95), 6);

        var r = await Compactador().CompactarAsync("foto.jpg", "image/jpeg", deitada, CancellationToken.None);

        // Mesmo sem ganho de tamanho, a de pé é a que vale guardar: o JPEG novo não leva o EXIF.
        r.Conteudo.Should().NotBeSameAs(deitada);
        using var dePe = Decodificar(r.Conteudo);
        dePe.Width.Should().Be(20);
        dePe.Height.Should().Be(60);
        dePe.GetPixel(10, 5).Red.Should().BeGreaterThan(200);
        dePe.GetPixel(10, 55).Blue.Should().BeGreaterThan(200);
    }

    [Fact]
    public void Transparencia_do_png_vira_fundo_branco()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        var png = Codificar(bitmap, SKEncodedImageFormat.Png, 100);

        var r = CompressorImagem.CompactarNaEntrada(png);

        r.Should().NotBeNull();
        using var jpeg = Decodificar(r!.Conteudo);
        jpeg.GetPixel(5, 5).Red.Should().BeGreaterThan(240, "sem a composição sobre branco o fundo sairia preto");
    }

    [Fact]
    public async Task Pdf_usa_o_degrau_de_entrada_e_guarda_o_resultado_menor()
    {
        var original = new byte[100_000];
        var menor = new byte[30_000];
        var compressor = new CompressorPdfFalso(menor);
        var compactador = new CompactadorAnexo(compressor, NullLogger<CompactadorAnexo>.Instance);

        var r = await compactador.CompactarAsync("pedido.pdf", "application/pdf", original, CancellationToken.None);

        compressor.Degrau.Should().Be(EscadaCompressaoPdf.Entrada);
        r.Conteudo.Should().BeSameAs(menor);
        r.Nome.Should().Be("pedido.pdf");
        r.TamanhoOriginal.Should().Be(100_000);
    }

    [Fact]
    public async Task Pdf_que_quase_nao_diminui_ou_que_o_ghostscript_recusa_vai_como_chegou()
    {
        var original = new byte[100_000];

        var semGanho = await Compactador(new byte[95_000])
            .CompactarAsync("laudo.pdf", "application/pdf", original, CancellationToken.None);
        var recusado = await Compactador(respostaPdf: null)
            .CompactarAsync("laudo.pdf", "application/pdf", original, CancellationToken.None);

        semGanho.Conteudo.Should().BeSameAs(original, "PDF já digital não ganha nada recomprimindo");
        recusado.Conteudo.Should().BeSameAs(original, "falha na compactação nunca bloqueia o anexo");
    }

    [Fact]
    public async Task Imagem_que_nao_decodifica_vai_como_chegou()
    {
        var lixo = "isto não é uma imagem"u8.ToArray();

        var r = await Compactador().CompactarAsync("foto.jpg", "image/jpeg", lixo, CancellationToken.None);

        r.Conteudo.Should().BeSameAs(lixo);
    }
}
