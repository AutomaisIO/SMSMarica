using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMais.Core.Integracoes.EsusSgWeb;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMais.Core.GuiasAgendamento;

/// <summary>
/// A guia do agendamento do ESUS de São Gonçalo (ADR-0076). O ESUS não tem PDF: o front dele monta
/// um cupom de impressora térmica com os dados do comprovante. Aqui vão os MESMOS campos, numa página
/// A5, e o rodapé diz que o documento foi montado a partir do ESUS — não é o papel de São Gonçalo.
/// </summary>
public static class GuiaEsusSgPdf
{
    public static byte[] Gerar(EsusSgComprovanteExame c, DateTime geradoEmBrasilia)
    {
        var documento = QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(12, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Helvetica"));

                page.Content().Column(col =>
                {
                    col.Spacing(3);
                    col.Item().Text("COMPROVANTE DE AGENDAMENTO DE EXAME").Bold().FontSize(11).AlignCenter();
                    if (!string.IsNullOrWhiteSpace(c.Municipio))
                    {
                        col.Item().Text($"ESUS — {c.Municipio}").FontSize(9).FontColor(Colors.Grey.Darken1).AlignCenter();
                    }

                    Secao(col, "Local");
                    Linha(col, null, c.Unidade, negrito: true);
                    Linha(col, null, c.Endereco);
                    Linha(col, "Referência", c.Referencia);

                    Secao(col, "Comparecer ao exame");
                    var quando = string.Join(" às ", new[] { c.DataExame, c.HoraExame }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    Linha(col, "Data", quando, negrito: true);

                    Secao(col, "Paciente");
                    Linha(col, "Paciente", c.Paciente);
                    Linha(col, "Nome social", c.NomeSocial);
                    Linha(col, "Prontuário", c.Prontuario);
                    Linha(col, "Pront. único", c.ProntuarioUnico);
                    Linha(col, "Cartão SUS", c.CartaoSus);

                    Secao(col, "Exame");
                    Linha(col, "Setor", c.Setor);
                    Linha(col, "Local", c.Local);
                    Linha(col, "Procedimento", c.Procedimento);
                    Linha(col, "Subprocedimentos", c.Subprocedimentos);
                    Linha(col, "Profissional", c.Profissional?.ToUpperInvariant());
                    Linha(col, "Ocupação", c.Ocupacao?.ToUpperInvariant());
                    if (!string.IsNullOrWhiteSpace(c.SenhaEfetivacao))
                    {
                        Linha(col, "Senha de efetivação", c.SenhaEfetivacao, negrito: true);
                    }
                    if (!string.IsNullOrWhiteSpace(c.Observacoes))
                    {
                        col.Item().PaddingTop(4).Text(c.Observacoes).Bold();
                    }

                    if (!string.IsNullOrWhiteSpace(c.RecomendacaoPreparo) || c.PreparoSubprocedimentos.Count > 0)
                    {
                        Secao(col, "Preparo");
                        if (!string.IsNullOrWhiteSpace(c.RecomendacaoPreparo))
                        {
                            col.Item().Text(c.RecomendacaoPreparo);
                        }
                        foreach (var (sub, preparo) in c.PreparoSubprocedimentos)
                        {
                            Linha(col, sub, preparo);
                        }
                    }

                    Secao(col, "Marcação");
                    Linha(col, "Marcado em", c.MarcadoEm);
                    Linha(col, "Por", c.MarcadoPor);
                    Linha(col, null, c.AgendadoPelaFila);
                    if (!string.IsNullOrWhiteSpace(c.Regulador))
                    {
                        Linha(col, (c.RotuloRegulador ?? "Profissional regulador").TrimEnd(':', ' '), c.Regulador);
                    }
                });

                page.Footer().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4)
                    .Text($"Montado em {geradoEmBrasilia:dd/MM/yyyy HH:mm} a partir dos dados do comprovante do ESUS de São Gonçalo.")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });

        using var ms = new MemoryStream();
        documento.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static void Secao(ColumnDescriptor col, string titulo) =>
        col.Item().PaddingTop(8).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1)
            .Text(titulo.ToUpperInvariant()).FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);

    private static void Linha(ColumnDescriptor col, string? rotulo, string? valor, bool negrito = false)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;
        col.Item().Text(t =>
        {
            if (!string.IsNullOrWhiteSpace(rotulo)) t.Span($"{rotulo}: ").FontColor(Colors.Grey.Darken2);
            var v = t.Span(valor);
            if (negrito) v.Bold();
        });
    }
}
