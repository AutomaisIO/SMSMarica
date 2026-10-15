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
public sealed record AnexoParaSer(
    string Nome, string ContentType, byte[] Conteudo, IReadOnlyList<Guid> Origens, IReadOnlyList<string> NomesOrigem);

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
/// <para>Arquivo acima de 5 MB é <b>comprimido automaticamente</b> antes de recusar (ticket #167):
/// imagem (foto de celular) é reencodada menor até caber, com piso de legibilidade; o PDF agrupado,
/// se estourar, é refeito comprimindo as imagens-fonte. Só quando nem comprimido cabe — ou quando é
/// um PDF avulso enviado pela unidade, que não dá para recomprimir com segurança aqui — é que recusa,
/// com o nome do arquivo, antes de tocar no SER.</para>
/// </summary>
public static class AnexosParaSer
{
    public const int MaximoDeArquivos = 2;
    public const long TamanhoMaximo = 5L * 1024 * 1024;

    public static IReadOnlyList<AnexoParaSer> Preparar(IReadOnlyList<ArquivoLido> arquivos, string sistema = "SER")
    {
        if (arquivos.Count == 0) return [];

        return arquivos.Count <= MaximoDeArquivos
            ? [.. arquivos.Select(a => UmArquivo(a, sistema))]
            : [AgrupadoEmPdf(arquivos, sistema)];
    }

    /// <summary>
    /// Um anexo que vai sozinho: o próprio arquivo. Se passar de 5 MB e for imagem, reencoda menor até
    /// caber (vira JPEG); PDF/outros acima de 5 MB não dá para comprimir com segurança aqui — recusa.
    /// </summary>
    private static AnexoParaSer UmArquivo(ArquivoLido a, string sistema)
    {
        var nome = NomeParaOSer(a);
        var conteudo = a.Conteudo;
        var contentType = a.ContentType;

        if (conteudo.LongLength > TamanhoMaximo)
        {
            var menor = EhImagem(a) ? CompressorImagem.ComprimirAte(conteudo, TamanhoMaximo) : null;
            if (menor is null)
            {
                throw new ValidacaoException(
                    "anexos",
                    $"O arquivo \"{nome}\" tem {Mb(conteudo.LongLength)} — o {sistema} aceita até 5 MB por arquivo. "
                    + (EhImagem(a)
                        ? "Mesmo comprimido não coube sem ficar ilegível; envie uma versão menor."
                        : "Substitua por uma versão menor (PDF comprimido) ou separe o conteúdo."));
            }
            conteudo = menor;
            contentType = "image/jpeg";
            nome = Path.ChangeExtension(nome, ".jpg");
        }

        return new AnexoParaSer(nome, contentType, conteudo, [a.Id], [NomeParaOSer(a)]);
    }

    /// <summary>
    /// 3+ arquivos viram um PDF só (o "documento" que a tela pede). Se o PDF estourar 5 MB, refaz
    /// comprimindo as imagens antes de juntar; se mesmo assim não couber (PDFs-fonte grandes, que não
    /// recomprimimos), recusa.
    /// </summary>
    private static AnexoParaSer AgrupadoEmPdf(IReadOnlyList<ArquivoLido> arquivos, string sistema)
    {
        var pdf = JuntarEmPdf(arquivos, comprimirImagens: false);
        if (pdf.LongLength > TamanhoMaximo)
            pdf = JuntarEmPdf(arquivos, comprimirImagens: true);

        if (pdf.LongLength > TamanhoMaximo)
        {
            throw new ValidacaoException(
                "anexos",
                $"Os {arquivos.Count} anexos juntos num PDF dão {Mb(pdf.LongLength)} mesmo comprimidos — o {sistema} "
                + "aceita até 5 MB por arquivo. Remova anexos ou envie um PDF menor no lugar.");
        }

        return new AnexoParaSer(
            "anexos-da-solicitacao.pdf", "application/pdf", pdf,
            [.. arquivos.Select(a => a.Id)], [.. arquivos.Select(NomeParaOSer)]);
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
