using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using DomElement = AngleSharp.Dom.IElement;
using DomNode = AngleSharp.Dom.INode;
using DomText = AngleSharp.Dom.IText;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Core.Laudos.Pdf;

public sealed class LaudoPdfRenderer(ILaudosService laudos, IOptions<LaudosPdfOptions> options) : ILaudoPdfRenderer
{
    private readonly ILaudosService _laudos = laudos;
    private readonly LaudosPdfOptions _opt = options.Value;

    public async Task<byte[]> GerarAsync(Guid laudoId, bool incluirTarja = true, CancellationToken cancellationToken = default)
    {
        var laudo = await _laudos.CarregarParaPdfAsync(laudoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), laudoId);

        var dadosCabecalho = MontarCabecalhoPaciente(laudo);
        var dadosAssinatura = MontarBlocoAssinatura(laudo);
        var blocos = ParseHtmlParaBlocos(laudo.ConteudoHtml);
        var emitidoEm = FormatarEmissao(laudo.FinalizadoEm ?? laudo.CriadoEm);

        var documento = QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                page.Header().Element(c => RenderHeader(c));
                page.Content().Element(c => RenderContent(c, laudo, dadosCabecalho, blocos));
                page.Footer().Element(c => RenderFooter(c, dadosAssinatura, emitidoEm, incluirTarja));
            });
        });

        using var ms = new MemoryStream();
        documento.GeneratePdf(ms);
        return ms.ToArray();
    }

    // ------------------------ Header ------------------------

    private void RenderHeader(IContainer container)
    {
        container.Row(row =>
        {
            if (!string.IsNullOrWhiteSpace(_opt.CaminhoLogo) && File.Exists(_opt.CaminhoLogo))
            {
                row.ConstantItem(80).Image(_opt.CaminhoLogo).FitWidth();
                row.ConstantItem(12);
            }

            row.RelativeItem().Column(c =>
            {
                c.Item().Text(_opt.TituloInstituicao).Bold().FontSize(12);
                c.Item().Text(_opt.SubtituloServico).FontSize(10);
                if (!string.IsNullOrWhiteSpace(_opt.EnderecoLinha1))
                {
                    c.Item().Text(_opt.EnderecoLinha1).FontSize(8).Light();
                }
                if (!string.IsNullOrWhiteSpace(_opt.EnderecoLinha2))
                {
                    c.Item().Text(_opt.EnderecoLinha2).FontSize(8).Light();
                }
                if (!string.IsNullOrWhiteSpace(_opt.Telefone))
                {
                    c.Item().Text($"Tel.: {_opt.Telefone}").FontSize(8).Light();
                }
            });
        });
    }

    // ------------------------ Content ------------------------

    private void RenderContent(IContainer container, Laudo laudo, IEnumerable<(string Rotulo, string Valor)> cabecalhoPaciente, IReadOnlyList<BlocoHtml> blocos)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Spacing(8);

            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

            // Bloco paciente / exame
            col.Item().Column(p =>
            {
                p.Spacing(2);
                foreach (var (rotulo, valor) in cabecalhoPaciente)
                {
                    p.Item().Text(span =>
                    {
                        span.Span($"{rotulo}: ").SemiBold();
                        span.Span(valor);
                    });
                }
            });

            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

            // Título do laudo (status banner se rascunho)
            col.Item().Text(laudo.Titulo).Bold().FontSize(14).AlignCenter();
            if (laudo.Status != StatusLaudo.Finalizado)
            {
                col.Item().Text("RASCUNHO — não emitido")
                    .FontColor(Colors.Red.Darken2)
                    .FontSize(10)
                    .AlignCenter();
            }

            // Corpo
            col.Item().Column(corpo =>
            {
                corpo.Spacing(6);
                foreach (var bloco in blocos)
                {
                    RenderBloco(corpo.Item(), bloco);
                }
            });
        });
    }

    private static void RenderBloco(IContainer container, BlocoHtml bloco)
    {
        switch (bloco.Tipo)
        {
            case TipoBloco.Titulo1:
                container.Text(t => RenderInline(t, bloco.Spans, baseSize: 14, baseBold: true));
                break;
            case TipoBloco.Titulo2:
                container.Text(t => RenderInline(t, bloco.Spans, baseSize: 12, baseBold: true));
                break;
            case TipoBloco.Titulo3:
                container.Text(t => RenderInline(t, bloco.Spans, baseSize: 11, baseBold: true));
                break;
            case TipoBloco.Paragrafo:
                container.Text(t => RenderInline(t, bloco.Spans, baseSize: 10, baseBold: false));
                break;
            case TipoBloco.ItemLista:
                container.Row(r =>
                {
                    r.ConstantItem(14).Text(bloco.PrefixoLista ?? "•").FontSize(10);
                    r.RelativeItem().Text(t => RenderInline(t, bloco.Spans, baseSize: 10, baseBold: false));
                });
                break;
        }
    }

    private static void RenderInline(QuestPDF.Fluent.TextDescriptor td, IReadOnlyList<SpanInline> spans, float baseSize, bool baseBold)
    {
        if (spans.Count == 0)
        {
            td.Span(string.Empty);
            return;
        }

        foreach (var s in spans)
        {
            var item = td.Span(s.Texto).FontSize(baseSize);
            if (baseBold || s.Bold) item = item.SemiBold();
            if (s.Italic) item = item.Italic();
            if (s.Underline) item = item.Underline();
        }
    }

    // ------------------------ Footer ------------------------

    private void RenderFooter(IContainer container, IReadOnlyList<string> assinatura, string emitidoEm, bool incluirTarja)
    {
        container.Column(c =>
        {
            c.Spacing(4);

            c.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

            // Bloco do médico + tarja só na versão NÃO assinada (on-demand). Quando o PDF
            // está sendo preparado para assinar (incluirTarja=false), o rodapé fica sem o
            // bloco do médico — o carimbo da assinatura (rubrica + nome/CRM/RQE) entra no
            // lugar, estampado pelo Automais.Assinador.
            if (incluirTarja)
            {
                c.Item().AlignCenter().Column(a =>
                {
                    a.Spacing(1);
                    foreach (var linha in assinatura)
                    {
                        a.Item().AlignCenter().Text(linha).FontSize(10).SemiBold();
                    }
                    a.Item().AlignCenter().Text(emitidoEm).FontSize(8).Light();
                });

                c.Item().PaddingTop(6).AlignCenter().Text(_opt.TarjaRodape)
                    .FontSize(7)
                    .FontColor(Colors.Grey.Darken2)
                    .Italic();
            }

            c.Item().AlignCenter().Text(t =>
            {
                t.Span("Página ").FontSize(8);
                t.CurrentPageNumber().FontSize(8);
                t.Span(" / ").FontSize(8);
                t.TotalPages().FontSize(8);
            });
        });
    }

    // ------------------------ Helpers de dados ------------------------

    private static IReadOnlyList<(string Rotulo, string Valor)> MontarCabecalhoPaciente(Laudo l)
    {
        var lista = new List<(string, string)>(6);

        // TODO: paciente vive no hub FHIR — resolver nome/CPF/CNS/nascimento via
        // API antes de renderizar (hoje o Laudo só carrega o PacienteId).
        lista.Add(("Paciente", l.PacienteId.HasValue ? l.PacienteId.Value.ToString() : "Não vinculado"));

        lista.Add(("Study Instance UID", l.StudyInstanceUID));

        return lista;
    }

    private IReadOnlyList<string> MontarBlocoAssinatura(Laudo l)
    {
        var nome = l.MedicoNomeSnapshot ?? string.Empty;
        var crm = l.MedicoCrmSnapshot ?? string.Empty;
        var uf = l.MedicoUfCrmSnapshot ?? string.Empty;
        var rqe = l.MedicoRqeSnapshot;

        var linhas = new List<string>(2)
        {
            $"Dr(a). {nome}",
            string.IsNullOrWhiteSpace(rqe) ? $"CRM {uf}/{crm}" : $"CRM {uf}/{crm} — RQE {rqe}",
        };
        return linhas;
    }

    private string FormatarEmissao(DateTime utc)
    {
        var local = utc.AddHours(_opt.OffsetHorasParaExibicao);
        var sinal = _opt.OffsetHorasParaExibicao >= 0 ? "+" : "-";
        var horas = Math.Abs(_opt.OffsetHorasParaExibicao).ToString("D2");
        return $"Emitido em {local:dd/MM/yyyy HH:mm} (UTC{sinal}{horas}:00)";
    }

    private static string FormatarCpf(string cpf)
    {
        var d = new string([.. cpf.Where(char.IsDigit)]);
        return d.Length == 11 ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}" : cpf;
    }

    // ------------------------ Parser HTML → Blocos ------------------------

    private static IReadOnlyList<BlocoHtml> ParseHtmlParaBlocos(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [new BlocoHtml(TipoBloco.Paragrafo, [], null)];
        }

        var parser = new HtmlParser();
        var doc = parser.ParseDocument($"<div id=\"laudo-root\">{html}</div>");
        DomNode? root = doc.QuerySelector("#laudo-root") ?? (DomNode?)doc.Body ?? doc.DocumentElement;
        if (root is null)
        {
            return [new BlocoHtml(TipoBloco.Paragrafo, [new SpanInline(html, false, false, false)], null)];
        }

        var blocos = new List<BlocoHtml>();
        foreach (var filho in root.ChildNodes)
        {
            ColetarBlocos(filho, blocos, null);
        }
        if (blocos.Count == 0)
        {
            blocos.Add(new BlocoHtml(TipoBloco.Paragrafo, [new SpanInline(html, false, false, false)], null));
        }
        return blocos;
    }

    private static void ColetarBlocos(DomNode node, List<BlocoHtml> destino, string? prefixoLista)
    {
        if (node is DomText txt && !string.IsNullOrWhiteSpace(txt.TextContent))
        {
            destino.Add(new BlocoHtml(TipoBloco.Paragrafo,
                [new SpanInline(txt.TextContent, false, false, false)],
                prefixoLista));
            return;
        }

        if (node is not DomElement el) return;

        var tag = el.TagName.ToUpperInvariant();
        switch (tag)
        {
            case "P":
                destino.Add(new BlocoHtml(TipoBloco.Paragrafo, ColetarSpans(el), prefixoLista));
                break;
            case "H1":
                destino.Add(new BlocoHtml(TipoBloco.Titulo1, ColetarSpans(el), null));
                break;
            case "H2":
                destino.Add(new BlocoHtml(TipoBloco.Titulo2, ColetarSpans(el), null));
                break;
            case "H3":
            case "H4":
            case "H5":
            case "H6":
                destino.Add(new BlocoHtml(TipoBloco.Titulo3, ColetarSpans(el), null));
                break;
            case "UL":
                foreach (var li in el.Children.Where(c => string.Equals(c.TagName, "LI", StringComparison.OrdinalIgnoreCase)))
                {
                    destino.Add(new BlocoHtml(TipoBloco.ItemLista, ColetarSpans(li), "•"));
                }
                break;
            case "OL":
                var i = 1;
                foreach (var li in el.Children.Where(c => string.Equals(c.TagName, "LI", StringComparison.OrdinalIgnoreCase)))
                {
                    destino.Add(new BlocoHtml(TipoBloco.ItemLista, ColetarSpans(li), $"{i}."));
                    i++;
                }
                break;
            case "BR":
                destino.Add(new BlocoHtml(TipoBloco.Paragrafo, [new SpanInline(string.Empty, false, false, false)], null));
                break;
            default:
                // Container desconhecido (div, section, table, etc): desce recursivamente.
                foreach (var filho in el.ChildNodes)
                {
                    ColetarBlocos(filho, destino, prefixoLista);
                }
                break;
        }
    }

    private static IReadOnlyList<SpanInline> ColetarSpans(DomNode node, bool herdaBold = false, bool herdaItalic = false, bool herdaUnderline = false)
    {
        var spans = new List<SpanInline>();
        foreach (var filho in node.ChildNodes)
        {
            if (filho is DomText t)
            {
                if (!string.IsNullOrEmpty(t.TextContent))
                {
                    spans.Add(new SpanInline(t.TextContent, herdaBold, herdaItalic, herdaUnderline));
                }
                continue;
            }
            if (filho is DomElement el)
            {
                var tag = el.TagName.ToUpperInvariant();
                var bold = herdaBold || tag is "STRONG" or "B";
                var italic = herdaItalic || tag is "EM" or "I";
                var underline = herdaUnderline || tag is "U";

                if (tag == "BR")
                {
                    spans.Add(new SpanInline("\n", herdaBold, herdaItalic, herdaUnderline));
                    continue;
                }

                spans.AddRange(ColetarSpans(el, bold, italic, underline));
            }
        }
        return spans;
    }

    private enum TipoBloco { Paragrafo, Titulo1, Titulo2, Titulo3, ItemLista }

    private sealed record SpanInline(string Texto, bool Bold, bool Italic, bool Underline);

    private sealed record BlocoHtml(TipoBloco Tipo, IReadOnlyList<SpanInline> Spans, string? PrefixoLista);
}
