using FluentAssertions;
using PdfSharp.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Exames;
using Xunit;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Tests.Exames;

/// <summary>
/// Garante que o merge (PDFsharp) consegue importar PDFs gerados pelo QuestPDF —
/// premissa do PDF de exame completo (capa+imagens + laudo num documento só).
/// </summary>
public class PdfMergeTests
{
    private static byte[] GerarQuestPdf(string texto, int paginas)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return QuestDocument.Create(c =>
        {
            for (var i = 0; i < paginas; i++)
            {
                c.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin(2, Unit.Centimetre);
                    p.Content().Text($"{texto} {i + 1}");
                });
            }
        }).GeneratePdf();
    }

    [Fact]
    public void Concatenar_dois_pdfs_do_QuestPDF_soma_as_paginas()
    {
        var a = GerarQuestPdf("Capa/Imagens", 3);
        var b = GerarQuestPdf("Laudo", 2);

        var merged = PdfMerge.Concatenar(a, b);

        merged.Should().NotBeNullOrEmpty();
        using var ms = new MemoryStream(merged);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(5);
    }

    [Fact]
    public void Concatenar_ignora_entradas_vazias()
    {
        var a = GerarQuestPdf("Único", 1);

        var merged = PdfMerge.Concatenar(a, []);

        using var ms = new MemoryStream(merged);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(1);
    }
}
