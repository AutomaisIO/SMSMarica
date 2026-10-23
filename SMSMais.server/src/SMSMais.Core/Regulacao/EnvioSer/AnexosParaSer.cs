using System.Globalization;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>Um arquivo pronto para subir ao SER.</summary>
/// <param name="NomesOrigem">Os nomes (como o regulador os vê) dos arquivos que entraram neste anexo —
/// um só, ou vários quando 3+ viraram um PDF. É o que a prévia mostra para a pessoa confirmar o
/// agrupamento antes de enviar (ticket #167).</param>
/// <param name="TamanhoOriginal">Quando o arquivo foi comprimido para caber: o tamanho de antes. A prévia
/// mostra "comprimido de 9,9 MB" para o regulador saber que o que sobe não é o original.</param>
/// <param name="Compressao">Como foi comprimido ("200 dpi", "foto reduzida") — só quando foi.</param>
public sealed record AnexoParaSer(
    string Nome, string ContentType, byte[] Conteudo, IReadOnlyList<Guid> Origens, IReadOnlyList<string> NomesOrigem,
    long? TamanhoOriginal = null, string? Compressao = null);

/// <summary>Um arquivo da solicitação, já lido do armazenamento.</summary>
public sealed record ArquivoLido(Guid Id, string Nome, string? Titulo, string ContentType, byte[] Conteudo);

/// <summary>
/// Ajusta os anexos da solicitação à regra do SER, lida na própria tela de criação (07/10/2026):
/// <i>"no máximo dois arquivos por solicitação e cada arquivo permite tamanho máximo de 5MB. Caso
/// existam mais de duas imagens para anexar, favor inseri-las em um documento"</i>.
///
/// <para>Até dois arquivos vão como estão. Com mais, tudo vira <b>um PDF só</b> (imagens, uma por
/// página; PDFs, página a página) — é o "documento" que a tela pede, sem ninguém precisar montar à
/// mão.</para>
///
/// <para>Arquivo acima do limite é <b>comprimido automaticamente</b> antes de recusar: imagem (foto
/// de celular) é reencodada menor com SkiaSharp (ticket #167); PDF — avulso ou o agrupado — passa pelo
/// Ghostscript (<see cref="ICompressorPdf"/>), que reduz as imagens de dentro dele até caber. Só quando
/// nem comprimido cabe — ou quando o Ghostscript não está disponível neste servidor — é que recusa,
/// com o nome do arquivo, antes de tocar no SER. O que está no armazenamento continua intacto (desde
/// 23/10/2026 já é a cópia compactada na entrada, <see cref="Anexos.CompactadorAnexo"/>): só a cópia
/// que sobe é comprimida de novo.</para>
/// </summary>
public static class AnexosParaSer
{
    public const int MaximoDeArquivos = 2;

    /// <summary>
    /// O limite por arquivo quando o destino não tem um configurado: <b>4,5 MB</b> (4.718.592 bytes),
    /// abaixo dos "5 MB" que a tela do SER declara. Nunca se mediu se o limite do servidor deles é
    /// 5.000.000 ou 5.242.880 bytes — o padrão cabe nos dois, com folga para o envelope do upload. O
    /// limite de cada destino se configura na aba dele (<see cref="IRegrasAnexoDestinoService"/>).
    /// </summary>
    public const long TamanhoMaximoPadrao = 4_718_592;

    /// <summary>Sem compressor de PDF e no limite padrão: PDF acima do limite é recusado.</summary>
    public static IReadOnlyList<AnexoParaSer> Preparar(IReadOnlyList<ArquivoLido> arquivos, string sistema = "SER") =>
        PrepararAsync(arquivos, sistema, TamanhoMaximoPadrao, compressorPdf: null, CancellationToken.None)
            .GetAwaiter().GetResult();

    /// <param name="tamanhoMaximo">O limite por arquivo do destino, em bytes (configurado na aba dele).</param>
    /// <param name="compressorPdf">Comprime PDF acima do limite (Ghostscript). <c>null</c> = não há como
    /// comprimir PDF: o avulso grande é recusado e o agrupado cai na compressão das imagens-fonte.</param>
    public static async Task<IReadOnlyList<AnexoParaSer>> PrepararAsync(
        IReadOnlyList<ArquivoLido> arquivos, string sistema, long tamanhoMaximo, ICompressorPdf? compressorPdf,
        CancellationToken ct)
    {
        if (arquivos.Count == 0) return [];

        if (arquivos.Count > MaximoDeArquivos)
            return [await AgrupadoEmPdfAsync(arquivos, sistema, tamanhoMaximo, compressorPdf, ct)];

        var saida = new List<AnexoParaSer>(arquivos.Count);
        foreach (var a in arquivos) saida.Add(await UmArquivoAsync(a, sistema, tamanhoMaximo, compressorPdf, ct));
        return saida;
    }

    /// <summary>
    /// Um anexo que vai sozinho: o próprio arquivo. Acima do limite, imagem é reencodada menor até
    /// caber (vira JPEG) e PDF passa pelo Ghostscript; outros tipos acima do limite são recusados.
    /// </summary>
    private static async Task<AnexoParaSer> UmArquivoAsync(
        ArquivoLido a, string sistema, long limite, ICompressorPdf? compressorPdf, CancellationToken ct)
    {
        var nome = NomeParaOSer(a);
        if (a.Conteudo.LongLength <= limite)
            return new AnexoParaSer(nome, a.ContentType, a.Conteudo, [a.Id], [nome]);

        var acima = AcimaDoLimite($"O arquivo \"{nome}\" tem", a.Conteudo.LongLength, sistema, limite);

        if (EhImagem(a))
        {
            var menor = CompressorImagem.ComprimirAte(a.Conteudo, limite) ?? throw acima;
            return new AnexoParaSer(
                Path.ChangeExtension(nome, ".jpg"), "image/jpeg", menor, [a.Id], [nome],
                a.Conteudo.LongLength, "foto reduzida");
        }

        if (!EhPdf(a)) throw acima;

        var compressao = compressorPdf is null
            ? CompressaoPdf.Indisponivel
            : await compressorPdf.ComprimirAteAsync(a.Conteudo, limite, ct);
        if (compressao.Desfecho != DesfechoCompressaoPdf.Coube) throw acima;
        return new AnexoParaSer(
            nome, "application/pdf", compressao.Conteudo!, [a.Id], [nome], a.Conteudo.LongLength, compressao.Degrau);
    }

    /// <summary>
    /// A recusa por tamanho diz só o tamanho e o limite do destino — quem regula não precisa saber como
    /// a compressão tentou nem onde o limite se configura. O motivo técnico (Ghostscript fora, piso de
    /// 150 dpi) fica no log.
    /// </summary>
    private static ValidacaoException AcimaDoLimite(string oQue, long tamanho, string sistema, long limite) =>
        new("anexos", $"{oQue} {Mb(tamanho)}. O limite do {sistema} é {Mb(limite)} por arquivo.");

    /// <summary>
    /// 3+ arquivos viram um PDF só (o "documento" que a tela pede). Se o PDF estourar o limite, passa
    /// pelo Ghostscript; sem ele, refaz comprimindo as imagens-fonte antes de juntar. Se mesmo assim não
    /// couber, recusa.
    /// </summary>
    private static async Task<AnexoParaSer> AgrupadoEmPdfAsync(
        IReadOnlyList<ArquivoLido> arquivos, string sistema, long limite, ICompressorPdf? compressorPdf,
        CancellationToken ct)
    {
        var pdf = JuntarEmPdf(arquivos, comprimirImagens: false);
        long? original = null;
        string? como = null;

        if (pdf.LongLength > limite)
        {
            original = pdf.LongLength;
            var compressao = compressorPdf is null
                ? CompressaoPdf.Indisponivel
                : await compressorPdf.ComprimirAteAsync(pdf, limite, ct);
            switch (compressao.Desfecho)
            {
                case DesfechoCompressaoPdf.Coube:
                    pdf = compressao.Conteudo!;
                    como = compressao.Degrau;
                    break;
                case DesfechoCompressaoPdf.NaoCoube:
                    // O Ghostscript já desceu até o piso; reduzir só as imagens-fonte não faria melhor.
                    break;
                default:
                    pdf = JuntarEmPdf(arquivos, comprimirImagens: true);
                    como = "fotos reduzidas";
                    break;
            }
        }

        if (pdf.LongLength > limite)
            throw AcimaDoLimite($"Os {arquivos.Count} anexos juntos num PDF têm", pdf.LongLength, sistema, limite);

        return new AnexoParaSer(
            "anexos-da-solicitacao.pdf", "application/pdf", pdf,
            [.. arquivos.Select(a => a.Id)], [.. arquivos.Select(NomeParaOSer)], original, como);
    }

    /// <summary>
    /// O nome com que o arquivo aparece no SER para o regulador de lá: o título que a unidade deu
    /// (ex.: "RELATORIO MEDICO"), sem acento nem caractere especial, com a extensão do arquivo.
    /// Nome de foto de celular ("2026-10-02_004.jpg") não diz nada a quem regula.
    /// </summary>
    public static string NomeParaOSer(ArquivoLido a)
    {
        var extensao = Path.GetExtension(a.Nome);
        if (string.IsNullOrEmpty(extensao)) extensao = ExtensaoDe(a.ContentType);
        var baseNome = string.IsNullOrWhiteSpace(a.Titulo) ? Path.GetFileNameWithoutExtension(a.Nome) : a.Titulo!;
        var limpo = SemAcento(baseNome).Trim();
        if (limpo.Length == 0) limpo = "anexo";
        if (limpo.Length > 80) limpo = limpo[..80];
        return limpo + extensao.ToLowerInvariant();
    }

    /// <param name="comprimirImagens">Reencoda cada imagem menor (lado maior 2000 px, JPEG q70) antes
    /// de desenhá-la — para o PDF agrupado caber em 5 MB. Só é ligado na segunda tentativa, quando o
    /// PDF sem comprimir estourou; assim o caso comum (poucas imagens pequenas) mantém a qualidade.</param>
    private static byte[] JuntarEmPdf(IReadOnlyList<ArquivoLido> arquivos, bool comprimirImagens)
    {
        using var saida = new PdfDocument();
        foreach (var a in arquivos)
        {
            if (EhPdf(a))
            {
                using var ms = new MemoryStream(a.Conteudo);
                using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
                for (var i = 0; i < doc.PageCount; i++) saida.AddPage(doc.Pages[i]);
                continue;
            }

            if (!EhImagem(a))
            {
                throw new ValidacaoException(
                    "anexos",
                    $"O anexo \"{a.Nome}\" ({a.ContentType}) não é imagem nem PDF — com mais de dois anexos eles são "
                    + "juntados num PDF, e este tipo não dá para juntar. Envie-o em PDF.");
            }

            var bytesImagem = a.Conteudo;
            if (comprimirImagens && CompressorImagem.Reencodar(a.Conteudo, 2000, 70) is { } reduzida)
                bytesImagem = reduzida;

            // Uma imagem por página A4, cabendo inteira e centralizada.
            // `publiclyVisible`: o PDFsharp lê o buffer interno do stream — sem isto, recusa a imagem.
            using var imagemMs = new MemoryStream(bytesImagem, 0, bytesImagem.Length, writable: false, publiclyVisible: true);
            using var imagem = XImage.FromStream(imagemMs);
            var pagina = saida.AddPage();
            pagina.Width = XUnit.FromMillimeter(210);
            pagina.Height = XUnit.FromMillimeter(297);
            using var gfx = XGraphics.FromPdfPage(pagina);
            var margem = XUnit.FromMillimeter(10).Point;
            var larguraUtil = pagina.Width.Point - 2 * margem;
            var alturaUtil = pagina.Height.Point - 2 * margem;
            var escala = Math.Min(larguraUtil / imagem.PointWidth, alturaUtil / imagem.PointHeight);
            var w = imagem.PointWidth * escala;
            var h = imagem.PointHeight * escala;
            gfx.DrawImage(imagem, (pagina.Width.Point - w) / 2, (pagina.Height.Point - h) / 2, w, h);
        }

        using var outMs = new MemoryStream();
        saida.Save(outMs);
        return outMs.ToArray();
    }

    private static bool EhPdf(ArquivoLido a) =>
        a.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || a.Nome.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool EhImagem(ArquivoLido a) =>
        a.ContentType.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || a.ContentType.StartsWith("image/png", StringComparison.OrdinalIgnoreCase);

    private static string ExtensaoDe(string contentType) => contentType.ToLowerInvariant() switch
    {
        var c when c.Contains("pdf") => ".pdf",
        var c when c.Contains("jpeg") => ".jpg",
        var c when c.Contains("png") => ".png",
        _ => string.Empty,
    };

    private static string SemAcento(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Mb(long bytes) => $"{bytes / 1024d / 1024d:0.0} MB".Replace('.', ',');
}
