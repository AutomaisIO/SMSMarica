using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace SMSMais.Core.Laudos.Assinatura;

/// <summary>
/// Estampa o carimbo do médico SEM certificado (ADR-0061): desenha a imagem por cima do
/// PDF-base fixado, na página e no retângulo que a médica escolheu. Não há assinatura nem
/// criptografia, então não passa pelo <c>Automais.Assinador</c> — aquele serviço só existe
/// para assinar. Usa o PDFsharp (MIT), que o servidor já tem para juntar PDFs de exame.
///
/// <para>
/// Mesma geometria do carimbo assinado: retângulo em pontos PDF com origem no canto
/// inferior-esquerdo, preso aos limites da página, e a imagem encaixada nele mantendo a
/// proporção, centralizada. Sem posição, cai no padrão legado (quadrado de 130pt
/// centralizado, 28pt acima do pé da última página).
/// </para>
/// </summary>
public static class CarimboPdf
{
    private const double LadoPadrao = 130d;
    private const double RecuoPePadrao = 28d;
    private const double LadoMinimo = 24d;

    public static byte[] Estampar(byte[] pdf, byte[] carimboPng, CarimboPosicaoPdf? posicao)
    {
        using var entrada = new MemoryStream(pdf);
        using var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);

        var total = doc.PageCount;
        var indice = (posicao is { } p ? Math.Clamp(p.Pagina, 1, total) : total) - 1;
        var pagina = doc.Pages[indice];
        var largPag = pagina.Width.Point;
        var altPag = pagina.Height.Point;

        var (x, y, w, h) = posicao is { } pos
            ? Clamp(pos, largPag, altPag)
            : ((largPag - LadoPadrao) / 2d, RecuoPePadrao, LadoPadrao, LadoPadrao);

        // O PDFsharp lê a imagem pelo buffer interno do stream: precisa ser "publiclyVisible".
        using var imagemStream = new MemoryStream(carimboPng, 0, carimboPng.Length, writable: false, publiclyVisible: true);
        using var imagem = XImage.FromStream(imagemStream);
        var escala = Math.Min(w / imagem.PointWidth, h / imagem.PointHeight);
        var wi = imagem.PointWidth * escala;
        var hi = imagem.PointHeight * escala;
        var xi = x + (w - wi) / 2d;
        var yi = y + (h - hi) / 2d;

        using (var gfx = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append))
        {
            // XGraphics tem origem no topo; a posição vem com origem na base.
            gfx.DrawImage(imagem, xi, altPag - yi - hi, wi, hi);
        }

        using var saida = new MemoryStream();
        doc.Save(saida);
        return saida.ToArray();
    }

    private static (double X, double Y, double W, double H) Clamp(CarimboPosicaoPdf p, double largPag, double altPag)
    {
        var w = Math.Clamp(p.Largura, LadoMinimo, largPag);
        var h = Math.Clamp(p.Altura, LadoMinimo, altPag);
        var x = Math.Clamp(p.X, 0d, largPag - w);
        var y = Math.Clamp(p.Y, 0d, altPag - h);
        return (x, y, w, h);
    }
}
