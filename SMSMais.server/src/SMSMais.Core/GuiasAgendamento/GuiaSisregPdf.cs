using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMais.Core.GuiasAgendamento;

/// <summary>
/// A guia do agendamento do SISREG (ADR-0076): a ficha que o "Imprimir" do SISREG imprime, com a
/// chave de confirmação em destaque e as seções na ordem da tela. O SISREG não gera PDF — imprime
/// a página no navegador —, então o documento é montado aqui, e o rodapé diz isso.
/// </summary>
public static class GuiaSisregPdf
{
    public static byte[] Gerar(FichaSisreg ficha, DateTime geradoEmBrasilia)
    {
        var documento = QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(14, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Helvetica"));

                page.Content().Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text("SISREG — FICHA DO AGENDAMENTO").Bold().FontSize(12).AlignCenter();

                    if (!string.IsNullOrWhiteSpace(ficha.Chave))
                    {
                        col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Column(c =>
                        {
                            c.Item().Text("CHAVE DE CONFIRMAÇÃO").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                            c.Item().Text(ficha.Chave).FontSize(20).Bold();
                        });
                    }

                    foreach (var secao in ficha.Secoes)
                    {
                        if (!string.IsNullOrWhiteSpace(secao.Titulo))
                        {
                            col.Item().PaddingTop(6).Background(Colors.Grey.Lighten3).PaddingHorizontal(4).PaddingVertical(2)
                                .Text(secao.Titulo.ToUpperInvariant()).FontSize(8.5f).SemiBold();
                        }

                        if (secao.Campos.Count > 0)
                        {
                            col.Item().Table(tabela =>
                            {
                                tabela.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });
                                foreach (var campo in secao.Campos)
                                {
                                    tabela.Cell().PaddingVertical(2).PaddingRight(6).Column(c =>
                                    {
                                        c.Item().Text(campo.Rotulo).FontSize(7).FontColor(Colors.Grey.Darken1);
                                        c.Item().Text(campo.Valor);
                                    });
                                }
                            });
                        }

                        foreach (var texto in secao.Textos)
                        {
                            col.Item().Text(texto);
                        }
                    }
                });

                page.Footer().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Darken1));
                    t.Span($"Montado em {geradoEmBrasilia:dd/MM/yyyy HH:mm} a partir da ficha do SISREG.");
                    if (!string.IsNullOrWhiteSpace(ficha.ExtraidaEm)) t.Span($" {ficha.ExtraidaEm}.");
                    t.Span("  Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });

        using var ms = new MemoryStream();
        documento.GeneratePdf(ms);
        return ms.ToArray();
    }
}
