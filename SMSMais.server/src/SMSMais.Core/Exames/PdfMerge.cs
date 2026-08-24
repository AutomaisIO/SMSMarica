using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace SMSMais.Core.Exames;

/// <summary>Concatena PDFs (PDFsharp). Usado para juntar capa+imagens e laudo num único documento.</summary>
public static class PdfMerge
{
    public static byte[] Concatenar(params byte[][] pdfs)
    {
        using var saida = new PdfDocument();
        foreach (var bytes in pdfs)
        {
            if (bytes is not { Length: > 0 }) continue;
            using var ms = new MemoryStream(bytes);
            using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
            for (var i = 0; i < doc.PageCount; i++)
            {
                saida.AddPage(doc.Pages[i]);
            }
        }

        using var outMs = new MemoryStream();
        saida.Save(outMs);
        return outMs.ToArray();
    }
}
