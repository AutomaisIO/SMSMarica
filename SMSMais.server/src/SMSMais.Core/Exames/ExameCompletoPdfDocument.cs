using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMais.Core.Exames;

/// <summary>Dados da capa do PDF do exame completo (capa rica + imagens; o laudo é anexado depois).</summary>
public sealed record ExameCompletoCapa(
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    DateOnly? PacienteNascimento,
    string ExameNome,
    string? Modalidade,
    string? Unidade,
    string? UnidadeSolicitante,
    string Accession,
    DateTime? RealizadoEm,
    DateTime SolicitadaEm,
    string? SolicitanteNome,
    bool IncluiLaudo,
    string? Justificativa,
    string? Observacoes);

/// <summary>
/// PDF do exame completo: capa institucional com todos os dados relevantes da
/// solicitação, seguida de uma página por imagem renderizada. O laudo, quando há,
/// é concatenado depois (PdfMerge), fora deste documento.
/// </summary>
public sealed class ExameCompletoPdfDocument(
    ExameCompletoCapa capa,
    IReadOnlyList<byte[]> imagens,
    IdentidadeVisualPdf idv)
{
    private string CorMarca => idv.CorPrimaria;
    private string CorEscura => idv.CorEscura;
    private const string Tinta = "#2B2B2B";

    private void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Helvetica").FontColor(Tinta));
            page.Content().Element(RenderCapa);
        });

        var total = imagens.Count;
        for (var i = 0; i < total; i++)
        {
            var img = imagens[i];
            var indice = i + 1;
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Helvetica").FontColor(Colors.Grey.Darken1));
                page.Content().Image(img).FitArea();
                page.Footer().AlignCenter().Text($"{capa.ExameNome}  ·  imagem {indice} de {total}");
            });
        }
    }

    private void RenderCapa(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(0);

            col.Item().Row(row =>
            {
                if (idv.Logo is not null)
                    row.ConstantItem(150).Height(54).Image(idv.Logo).FitArea();
                row.RelativeItem().AlignRight().AlignMiddle().Text(idv.Nome)
                    .FontSize(16).Bold().FontColor(CorMarca);
            });

            col.Item().PaddingTop(6).LineHorizontal(2).LineColor(CorMarca);

            col.Item().PaddingTop(24).Text("Exame de Imagem").FontSize(22).Bold().FontColor(CorEscura);
            col.Item().PaddingTop(2).Text("Documento consolidado: dados do exame, imagens e laudo.")
                .FontSize(10).FontColor(Colors.Grey.Darken1);

            // Paciente
            col.Item().PaddingTop(24).Element(c => Secao(c, "Paciente"));
            col.Item().PaddingTop(8).Column(c =>
            {
                c.Spacing(4);
                Linha(c, "Nome", capa.PacienteNome);
                if (!string.IsNullOrWhiteSpace(capa.PacienteCpf)) Linha(c, "CPF", FormatarCpf(capa.PacienteCpf!));
                if (!string.IsNullOrWhiteSpace(capa.PacienteCns)) Linha(c, "Cartão SUS", capa.PacienteCns!);
                if (capa.PacienteNascimento is { } nasc) Linha(c, "Nascimento", nasc.ToString("dd/MM/yyyy"));
            });

            // Exame
            col.Item().PaddingTop(20).Element(c => Secao(c, "Exame"));
            col.Item().PaddingTop(8).Column(c =>
            {
                c.Spacing(4);
                Linha(c, "Exame", capa.ExameNome);
                if (!string.IsNullOrWhiteSpace(capa.Modalidade)) Linha(c, "Modalidade", capa.Modalidade!);
                if (!string.IsNullOrWhiteSpace(capa.Unidade)) Linha(c, "Unidade executora", capa.Unidade!);
                if (!string.IsNullOrWhiteSpace(capa.UnidadeSolicitante)) Linha(c, "Unidade solicitante", capa.UnidadeSolicitante!);
                Linha(c, "Accession (PACS)", capa.Accession);
                if (capa.RealizadoEm is { } real) Linha(c, "Realizado em", real.ToString("dd/MM/yyyy HH:mm"));
                Linha(c, "Solicitado em", capa.SolicitadaEm.ToString("dd/MM/yyyy"));
                Linha(c, "Imagens", imagens.Count.ToString());
            });

            // Solicitante
            if (!string.IsNullOrWhiteSpace(capa.SolicitanteNome))
            {
                col.Item().PaddingTop(20).Element(c => Secao(c, "Solicitante"));
                col.Item().PaddingTop(8).Column(c =>
                {
                    c.Spacing(4);
                    Linha(c, "Nome", capa.SolicitanteNome!);
                });
            }

            if (!string.IsNullOrWhiteSpace(capa.Justificativa))
            {
                col.Item().PaddingTop(20).Element(c => Secao(c, "Indicação clínica"));
                col.Item().PaddingTop(8).Text(capa.Justificativa!).FontSize(10).LineHeight(1.4f);
            }

            if (!string.IsNullOrWhiteSpace(capa.Observacoes))
            {
                col.Item().PaddingTop(16).Element(c => Secao(c, "Observações"));
                col.Item().PaddingTop(8).Text(capa.Observacoes!).FontSize(10).LineHeight(1.4f);
            }

            col.Item().PaddingTop(24).Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Darken1));
                t.Span("Conteúdo deste documento: ").SemiBold();
                t.Span(imagens.Count > 0 ? $"capa, {imagens.Count} imagem(ns)" : "capa");
                t.Span(capa.IncluiLaudo ? " e laudo médico." : ". Laudo ainda não disponível.");
            });

            col.Item().PaddingTop(8).Text(
                "As imagens são uma representação visual do exame e não substituem o laudo médico.")
                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
        });
    }

    private void Secao(IContainer container, string titulo) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(3)
            .Text(titulo.ToUpperInvariant()).FontSize(9).Bold().FontColor(CorMarca).LetterSpacing(0.05f);

    private static void Linha(ColumnDescriptor col, string rotulo, string valor) =>
        col.Item().Row(row =>
        {
            row.ConstantItem(130).Text(rotulo).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().Text(valor).SemiBold();
        });

    private static string FormatarCpf(string cpf)
    {
        var d = new string([.. cpf.Where(char.IsDigit)]);
        return d.Length == 11 ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}" : cpf;
    }

    public byte[] Gerar()
    {
        using var ms = new MemoryStream();
        QuestDocument.Create(Compose).GeneratePdf(ms);
        return ms.ToArray();
    }
}
