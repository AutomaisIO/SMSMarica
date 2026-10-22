using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.EnvioSer;

namespace SMSMais.Tests.Regulacao.EnvioSer;

/// <summary>Roda só onde houver Ghostscript (a VM de produção, ou <c>GHOSTSCRIPT_TESTE</c> apontando para ele).</summary>
public sealed class FactComGhostscriptAttribute : FactAttribute
{
    public static string Caminho => Environment.GetEnvironmentVariable("GHOSTSCRIPT_TESTE") ?? "/usr/bin/gs";

    public FactComGhostscriptAttribute()
    {
        if (!File.Exists(Caminho)) Skip = $"Ghostscript não encontrado em {Caminho} (defina GHOSTSCRIPT_TESTE).";
    }
}

/// <summary>
/// PDF acima do limite do SER/SERNIT passa pelo Ghostscript antes de subir (21/10/2026): a regra em
/// <see cref="AnexosParaSer"/>, a escada de degraus e os argumentos que mantêm o processo contido.
/// </summary>
public class CompressaoPdfTests
{
    private sealed class CompressorFalso(CompressaoPdf resposta) : ICompressorPdf
    {
        public int Chamadas { get; private set; }
        public long? Alvo { get; private set; }

        public Task<CompressaoPdf> ComprimirAteAsync(byte[] pdf, long alvoBytes, CancellationToken ct)
        {
            Chamadas++;
            Alvo = alvoBytes;
            return Task.FromResult(resposta);
        }
    }

    private static readonly byte[] PdfPequeno = "%PDF-1.4 comprimido"u8.ToArray();

    private static byte[] ComCabecalhoPdf(int tamanho)
    {
        var bytes = new byte[tamanho];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        return bytes;
    }

    private static ArquivoLido Pdf(string titulo, byte[] conteudo) =>
        new(Guid.NewGuid(), "scan.pdf", titulo, "application/pdf", conteudo);

    /// <summary>Uma foto de ruído — não comprime sem perda, então estoura o limite com folga.</summary>
    private static byte[] PngRuido(int lado = 1500)
    {
        using var bmp = new SKBitmap(lado, lado);
        var rnd = new Random(7);
        for (var y = 0; y < lado; y++)
            for (var x = 0; x < lado; x++)
                bmp.SetPixel(x, y, new SKColor((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)));
        using var img = SKImage.FromBitmap(bmp);
        using var dados = img.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    /// <summary>Um PDF de verdade, uma página A4 com a imagem — o "escaneado" que a unidade anexa.</summary>
    private static byte[] PdfEscaneado(byte[] imagem)
    {
        using var doc = new PdfDocument();
        var pagina = doc.AddPage();
        pagina.Width = XUnit.FromMillimeter(210);
        pagina.Height = XUnit.FromMillimeter(297);
        using (var gfx = XGraphics.FromPdfPage(pagina))
        using (var ms = new MemoryStream(imagem, 0, imagem.Length, writable: false, publiclyVisible: true))
        using (var img = XImage.FromStream(ms))
        {
            var lado = pagina.Width.Point - 40;
            gfx.DrawImage(img, 20, 20, lado, lado);
        }
        using var saida = new MemoryStream();
        doc.Save(saida);
        return saida.ToArray();
    }

    private static ArquivoLido Foto(string titulo, byte[] png) =>
        new(Guid.NewGuid(), "foto.png", titulo, "image/png", png);

    [Fact]
    public void Alvo_fica_abaixo_dos_5MB_nas_duas_leituras_do_limite()
    {
        // Nunca se mediu se o "5 MB" do SER é 5.000.000 ou 5.242.880 bytes.
        AnexosParaSer.TamanhoMaximoPadrao.Should().BeLessThan(5_000_000);
    }

    [Fact]
    public async Task Pdf_avulso_acima_do_limite_vai_comprimido_e_a_previa_sabe_de_quanto()
    {
        var compressor = new CompressorFalso(new CompressaoPdf(DesfechoCompressaoPdf.Coube, PdfPequeno, "200 dpi"));
        var pedido = Pdf("PEDIDO MÉDICO", ComCabecalhoPdf(6_000_000));

        var saida = await AnexosParaSer.PrepararAsync(
            [pedido], "SERNIT", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        saida.Should().ContainSingle();
        saida[0].Nome.Should().Be("PEDIDO MEDICO.pdf");
        saida[0].ContentType.Should().Be("application/pdf");
        saida[0].Conteudo.Should().Equal(PdfPequeno);
        saida[0].TamanhoOriginal.Should().Be(6_000_000);
        saida[0].Compressao.Should().Be("200 dpi");
        compressor.Alvo.Should().Be(AnexosParaSer.TamanhoMaximoPadrao, "sem configuração, vale o padrão");
    }

    [Fact]
    public async Task O_limite_configurado_no_destino_e_o_que_decide_comprimir()
    {
        var pedido = Pdf("PEDIDO", ComCabecalhoPdf(3_000_000));
        var compressor = new CompressorFalso(new CompressaoPdf(DesfechoCompressaoPdf.Coube, PdfPequeno, "200 dpi"));

        // 3 MB cabem no padrão (4,5 MB): vai como está.
        await AnexosParaSer.PrepararAsync([pedido], "SER", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);
        compressor.Chamadas.Should().Be(0);

        // Com o SER configurado para 2 MB, os mesmos 3 MB passam a ser comprimidos — e o alvo é os 2 MB.
        const long doisMb = 2 * 1024 * 1024;
        var saida = await AnexosParaSer.PrepararAsync([pedido], "SER", doisMb, compressor, CancellationToken.None);
        compressor.Chamadas.Should().Be(1);
        compressor.Alvo.Should().Be(doisMb);
        saida[0].TamanhoOriginal.Should().Be(3_000_000);
    }

    [Fact]
    public async Task A_recusa_diz_so_o_tamanho_e_o_limite_do_destino()
    {
        var acao = () => AnexosParaSer.PrepararAsync(
            [Pdf("PEDIDO", ComCabecalhoPdf(3_000_000))], "SERNIT", 2 * 1024 * 1024,
            new CompressorFalso(CompressaoPdf.NaoCoube), CancellationToken.None);

        // Quem regula não precisa saber como a compressão tentou nem onde o limite se configura.
        var erro = await acao.Should().ThrowAsync<ValidacaoException>();
        erro.Which.Message.Should().Be("O arquivo \"PEDIDO.pdf\" tem 2,9 MB. O limite do SERNIT é 2,0 MB por arquivo.");
    }

    [Fact]
    public async Task Pdf_dentro_do_limite_nem_passa_pelo_compressor()
    {
        var compressor = new CompressorFalso(CompressaoPdf.Falhou);

        var saida = await AnexosParaSer.PrepararAsync(
            [Pdf("PEDIDO", ComCabecalhoPdf(1_000_000))], "SER", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        compressor.Chamadas.Should().Be(0);
        saida[0].TamanhoOriginal.Should().BeNull();
        saida[0].Compressao.Should().BeNull();
    }

    [Fact]
    public async Task Pdf_que_nem_comprimido_cabe_e_recusado_com_o_limite()
    {
        var compressor = new CompressorFalso(CompressaoPdf.NaoCoube);

        var acao = () => AnexosParaSer.PrepararAsync(
            [Pdf("PEDIDO MÉDICO", ComCabecalhoPdf(6_000_000))], "SERNIT", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>().WithMessage("*PEDIDO MEDICO.pdf*limite do SERNIT é 4,5 MB*");
    }

    [Fact]
    public async Task Sem_ghostscript_pdf_grande_e_recusado_antes_de_tocar_o_sistema()
    {
        var compressor = new CompressorFalso(CompressaoPdf.Indisponivel);

        var acao = () => AnexosParaSer.PrepararAsync(
            [Pdf("EXAME", ComCabecalhoPdf(6_000_000))], "SER", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>().WithMessage("*EXAME.pdf*limite do SER é 4,5 MB*");
    }

    [Fact]
    public async Task Agrupado_que_estoura_passa_pelo_ghostscript()
    {
        using var pequeno = new MemoryStream();
        using (var doc = new PdfDocument()) { doc.AddPage(); doc.Save(pequeno); }
        var compressor = new CompressorFalso(
            new CompressaoPdf(DesfechoCompressaoPdf.Coube, pequeno.ToArray(), "170 dpi"));
        var grande = Pdf("LAUDO ANTERIOR", PdfEscaneado(PngRuido()));
        grande.Conteudo.LongLength.Should().BeGreaterThan(AnexosParaSer.TamanhoMaximoPadrao, "o PDF de ruído tem de estourar");

        var saida = await AnexosParaSer.PrepararAsync(
            [grande, Foto("A", PngRuido(10)), Foto("B", PngRuido(10))], "SER", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        saida.Should().ContainSingle();
        saida[0].Nome.Should().Be("anexos-da-solicitacao.pdf");
        saida[0].Conteudo.Should().Equal(pequeno.ToArray());
        saida[0].Compressao.Should().Be("170 dpi");
        saida[0].TamanhoOriginal.Should().BeGreaterThan(AnexosParaSer.TamanhoMaximoPadrao);
    }

    [Fact]
    public async Task Agrupado_sem_ghostscript_cai_na_reducao_das_fotos()
    {
        var compressor = new CompressorFalso(CompressaoPdf.Indisponivel);
        var fotos = new[] { Foto("A", PngRuido()), Foto("B", PngRuido()), Foto("C", PngRuido()) };

        var saida = await AnexosParaSer.PrepararAsync(
            fotos, "SER", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        saida.Should().ContainSingle();
        saida[0].Conteudo.LongLength.Should().BeLessThanOrEqualTo(AnexosParaSer.TamanhoMaximoPadrao);
        saida[0].Compressao.Should().Be("fotos reduzidas");
    }

    [Fact]
    public async Task Agrupado_que_nem_o_ghostscript_faz_caber_e_recusado()
    {
        var compressor = new CompressorFalso(CompressaoPdf.NaoCoube);
        var grande = Pdf("LAUDO ANTERIOR", PdfEscaneado(PngRuido()));

        var acao = () => AnexosParaSer.PrepararAsync(
            [grande, Foto("A", PngRuido(10)), Foto("B", PngRuido(10))], "SER", AnexosParaSer.TamanhoMaximoPadrao, compressor, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>().WithMessage("*3 anexos juntos*limite do SER é 4,5 MB*");
    }

    [Fact]
    public async Task Escada_escolhe_o_degrau_mais_fiel_que_cabe_em_ate_3_passadas()
    {
        long[] tamanhos = [9_000_000, 6_000_000, 4_000_000, 3_000_000, 2_000_000, 1_000_000];
        var tentados = new List<int>();

        var r = await EscadaCompressaoPdf.BuscarAsync(
            tamanhos.Length, (i, _) => { tentados.Add(i); return Task.FromResult<byte[]?>(new byte[tamanhos[i]]); },
            4_800_000, CancellationToken.None);

        r.Desfecho.Should().Be(DesfechoCompressaoPdf.Coube);
        r.Indice.Should().Be(2, "200 dpi/0,76 é o mais fiel abaixo de 4,8 MB");
        r.Passadas.Should().BeLessThanOrEqualTo(3);
        tentados.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Escada_com_tudo_cabendo_fica_no_degrau_mais_fiel()
    {
        var r = await EscadaCompressaoPdf.BuscarAsync(
            6, (_, _) => Task.FromResult<byte[]?>(new byte[1_000]), 4_800_000, CancellationToken.None);

        r.Indice.Should().Be(0);
    }

    [Fact]
    public async Task Escada_sem_degrau_que_caiba_devolve_nao_coube()
    {
        var r = await EscadaCompressaoPdf.BuscarAsync(
            6, (_, _) => Task.FromResult<byte[]?>(new byte[9_000_000]), 4_800_000, CancellationToken.None);

        r.Desfecho.Should().Be(DesfechoCompressaoPdf.NaoCoube);
        r.Conteudo.Should().BeNull();
    }

    [Fact]
    public async Task Escada_para_na_primeira_falha_do_ghostscript()
    {
        var r = await EscadaCompressaoPdf.BuscarAsync(
            6, (_, _) => Task.FromResult<byte[]?>(null), 4_800_000, CancellationToken.None);

        r.Desfecho.Should().Be(DesfechoCompressaoPdf.Falhou);
        r.Passadas.Should().Be(1);
    }

    [Fact]
    public void Argumentos_mantem_o_SAFER_recomprimem_o_JPEG_e_usam_ponto_decimal()
    {
        var antes = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            var args = CompressorPdfGhostscript.Argumentos(new DegrauPdf(150, 0.76), "/tmp/x/entrada.pdf", "/tmp/x/saida.pdf");

            args.Should().Contain("-dSAFER");
            args.Should().Contain("-dPassThroughJPEGImages=false", "sem isto o pdfwrite repassa o JPEG e quase não reduz");
            args.Should().Contain("-dColorImageResolution=150").And.Contain("-dGrayImageResolution=150");
            args.Should().Contain(a => a.Contains("/QFactor 0.76") && !a.Contains("0,76"));
            args.Should().Contain("-sOutputFile=/tmp/x/saida.pdf");
            args[^1].Should().Be("/tmp/x/entrada.pdf");
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    private static CompressorPdfGhostscript Compressor(string caminho) => new(
        Options.Create(new GhostscriptOptions { Caminho = caminho }),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<CompressorPdfGhostscript>.Instance);

    [Fact]
    public async Task Sem_o_executavel_a_compressao_fica_indisponivel()
    {
        using var compressor = Compressor(Path.Combine(Path.GetTempPath(), "nao-existe", "gs"));

        var r = await compressor.ComprimirAteAsync(ComCabecalhoPdf(10), 4_800_000, CancellationToken.None);

        r.Desfecho.Should().Be(DesfechoCompressaoPdf.Indisponivel);
    }

    [Fact]
    public async Task O_que_nao_comeca_com_PDF_nem_chega_ao_ghostscript()
    {
        // Um executável que existe mas não é o Ghostscript: se o arquivo chegasse a ele, o teste quebraria.
        using var compressor = Compressor(Environment.ProcessPath!);

        var r = await compressor.ComprimirAteAsync("%!PS-Adobe-3.0 showpage"u8.ToArray(), 4_800_000, CancellationToken.None);

        r.Desfecho.Should().Be(DesfechoCompressaoPdf.Falhou);
    }

    [FactComGhostscript]
    public async Task Ghostscript_de_verdade_faz_o_escaneado_caber_sem_perder_pagina()
    {
        using var compressor = Compressor(FactComGhostscriptAttribute.Caminho);
        var original = PdfEscaneado(PngRuido(2400));
        original.LongLength.Should().BeGreaterThan(AnexosParaSer.TamanhoMaximoPadrao);

        var r = await compressor.ComprimirAteAsync(original, AnexosParaSer.TamanhoMaximoPadrao, CancellationToken.None);

        r.Desfecho.Should().Be(DesfechoCompressaoPdf.Coube);
        r.Conteudo!.LongLength.Should().BeLessThanOrEqualTo(AnexosParaSer.TamanhoMaximoPadrao);
        r.Degrau.Should().EndWith("dpi");
        using var pdf = PdfReader.Open(new MemoryStream(r.Conteudo), PdfDocumentOpenMode.Import);
        pdf.PageCount.Should().Be(1);
    }
}
