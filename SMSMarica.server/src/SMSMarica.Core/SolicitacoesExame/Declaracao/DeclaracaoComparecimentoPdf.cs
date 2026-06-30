using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Core.SolicitacoesExame.Declaracao;

/// <summary>Dados já resolvidos (strings/imagem) para renderizar a declaração.</summary>
public sealed record DeclaracaoComparecimentoDados(
    string PacienteNome,
    string UnidadeNome,
    string TipoExameNome,
    DateTime DataHoraExame,
    string Cidade,
    DateTime DataEmissao,
    string AssinanteNome,
    byte[]? CabecalhoImagem,
    // Selo de autenticidade: QR (PNG) + código (UUID) + URL pública de verificação.
    byte[]? QrCode,
    string Codigo,
    string VerificacaoUrl);

/// <summary>
/// Renderizador puro (QuestPDF) da declaração de comparecimento. Página A5 retrato
/// (148 × 200 mm) usando a imagem configurada no cabeçalho do laudo no topo.
/// </summary>
public static class DeclaracaoComparecimentoPdf
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static byte[] Gerar(DeclaracaoComparecimentoDados d)
    {
        var documento = QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(148, 200, Unit.Millimetre);
                page.Margin(12, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Helvetica"));

                page.Content().Column(col =>
                {
                    if (d.CabecalhoImagem is { Length: > 0 } img)
                    {
                        col.Item().AlignCenter().Image(img).FitWidth();
                    }

                    col.Item().PaddingTop(28).Text("DECLARAÇÃO DE COMPARECIMENTO")
                        .Bold().FontSize(11).AlignCenter();

                    col.Item().PaddingTop(26).Text(texto =>
                    {
                        texto.Justify();
                        texto.DefaultTextStyle(s => s.FontSize(9).LineHeight(1.8f));

                        texto.Span("Declaro, a pedido da(o) usuária(o) ");
                        texto.Span(d.PacienteNome).Bold();
                        texto.Span(", que esta(e) compareceu à unidade de saúde ");
                        texto.Span(d.UnidadeNome).Bold();
                        texto.Span(" no dia ");
                        texto.Span(DataPorExtenso(d.DataHoraExame)).Bold();
                        texto.Span(" às ");
                        texto.Span(d.DataHoraExame.ToString("HH'h'mm", PtBr)).Bold();
                        texto.Span(", para a finalidade de realização de ");
                        texto.Span(d.TipoExameNome).Bold();
                        texto.Span(".");
                    });

                    col.Item().PaddingTop(34).AlignRight()
                        .Text($"{d.Cidade}, {DataPorExtenso(d.DataEmissao)}.")
                        .FontSize(9);
                });

                // Rodapé: assinatura centralizada + selo de autenticidade (QR) embaixo.
                page.Footer().PaddingBottom(16).Column(rodape =>
                {
                    rodape.Item().AlignCenter().Width(230).Column(assina =>
                    {
                        assina.Item().LineHorizontal(0.8f).LineColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(d.AssinanteNome))
                        {
                            assina.Item().PaddingTop(3).AlignCenter().Text(d.AssinanteNome).FontSize(9).SemiBold();
                        }
                        assina.Item().AlignCenter().Text("Assinatura e carimbo do profissional")
                            .FontSize(8).Light();
                    });

                    rodape.Item().PaddingTop(14).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                        .PaddingTop(6).Row(row =>
                        {
                            if (d.QrCode is { Length: > 0 } qr)
                            {
                                row.ConstantItem(58).Image(qr).FitWidth();
                            }
                            row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(t =>
                            {
                                t.Item().Text("Selo de autenticidade").FontSize(8).SemiBold();
                                t.Item().Text("Aponte a câmera para o QR Code para confirmar a validade deste documento.")
                                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                                t.Item().PaddingTop(1).Text(d.VerificacaoUrl).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                                t.Item().Text($"Código: {d.Codigo}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                            });
                        });
                });
            });
        });

        using var ms = new MemoryStream();
        documento.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static string DataPorExtenso(DateTime data)
    {
        var mes = PtBr.DateTimeFormat.GetMonthName(data.Month);
        return $"{data.Day} de {mes} de {data.Year}";
    }
}
