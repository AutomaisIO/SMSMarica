using FluentAssertions;
using PdfSharp.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Placeholders = QuestPDF.Helpers.Placeholders;
using SMSMais.Core.Exames;
using Xunit;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMais.Tests.Exames;

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
    public void Concatenar_pdf_do_QuestPDF_com_imagem_embutida()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var jpeg = Placeholders.Image(400, 300);
        var comImagem = QuestDocument.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(1, Unit.Centimetre);
                p.Content().Image(jpeg).FitArea();
            });
        }).GeneratePdf();

        var laudo = GerarQuestPdf("Laudo", 1);

        var merged = PdfMerge.Concatenar(comImagem, laudo);

        using var ms = new MemoryStream(merged);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(2);
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
