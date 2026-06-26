using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Core.Exames;

/// <summary>Dados da capa do PDF consolidado de imagens (frente do documento do cidadão).</summary>
public sealed record ExameImagensCapa(
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    DateOnly? PacienteNascimento,
    string ExameNome,
    DateTime? RealizadoEm,
    string? Unidade,
    string? Descricao,
    string? Anamnese);

/// <summary>
/// PDF consolidado das imagens de um estudo do PACS: uma capa institucional (logo + dados do
/// paciente + do exame) seguida de uma página por imagem renderizada (WADO-RS /rendered).
/// </summary>
public sealed class ExameImagensPdfDocument(
    ExameImagensCapa capa,
    IReadOnlyList<byte[]> imagens,
    byte[]? logo)
{
    private const string Marica = "#C4122F";
    private const string Vinho = "#7A0C24";
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

            // Faixa de marca
            col.Item().Row(row =>
            {
                if (logo is not null)
                    row.ConstantItem(150).Height(54).Image(logo).FitArea();
                row.RelativeItem().AlignRight().AlignMiddle().Text("Saúde Maricá")
                    .FontSize(16).Bold().FontColor(Marica);
            });

            col.Item().PaddingTop(6).LineHorizontal(2).LineColor(Marica);

            col.Item().PaddingTop(24).Text("Imagens do Exame")
                .FontSize(22).Bold().FontColor(Vinho);
            col.Item().PaddingTop(2).Text("Documento gerado para o cidadão a partir das imagens do exame.")
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
                if (capa.RealizadoEm is { } real) Linha(c, "Realizado em", real.ToString("dd/MM/yyyy"));
                if (!string.IsNullOrWhiteSpace(capa.Unidade)) Linha(c, "Local", capa.Unidade!);
                Linha(c, "Imagens", imagens.Count.ToString());
            });

            if (!string.IsNullOrWhiteSpace(capa.Descricao))
            {
                col.Item().PaddingTop(20).Element(c => Secao(c, "Descrição / Indicação clínica"));
                col.Item().PaddingTop(8).Text(capa.Descricao!).FontSize(10).LineHeight(1.4f);
            }

            if (!string.IsNullOrWhiteSpace(capa.Anamnese))
            {
                col.Item().PaddingTop(16).Element(c => Secao(c, "Anamnese (resumo)"));
                col.Item().PaddingTop(8).Text(capa.Anamnese!).FontSize(10).LineHeight(1.4f);
            }

            col.Item().PaddingTop(28).Text(
                "As imagens nas páginas seguintes são uma representação visual do exame e não substituem o laudo médico.")
                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
        });
    }

    private static void Secao(IContainer container, string titulo) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(3)
            .Text(titulo.ToUpperInvariant()).FontSize(9).Bold().FontColor(Marica).LetterSpacing(0.05f);

    private static void Linha(ColumnDescriptor col, string rotulo, string valor) =>
        col.Item().Row(row =>
        {
            row.ConstantItem(120).Text(rotulo).FontColor(Colors.Grey.Darken1);
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
