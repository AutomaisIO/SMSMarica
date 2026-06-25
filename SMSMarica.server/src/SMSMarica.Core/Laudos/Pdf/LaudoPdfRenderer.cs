using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.Midias;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using DomElement = AngleSharp.Dom.IElement;
using DomNode = AngleSharp.Dom.INode;
using DomText = AngleSharp.Dom.IText;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Core.Laudos.Pdf;

public sealed class LaudoPdfRenderer(
    ILaudosService laudos,
    ILaudoConfiguracaoService configuracao,
    IMidiasService midias,
    IOptions<LaudosPdfOptions> options) : ILaudoPdfRenderer
{
    private readonly ILaudosService _laudos = laudos;
    private readonly ILaudoConfiguracaoService _configuracao = configuracao;
    private readonly IMidiasService _midias = midias;
    private readonly LaudosPdfOptions _opt = options.Value;

    public async Task<byte[]> GerarAsync(
        Guid laudoId,
        ModoRodapeLaudo modo = ModoRodapeLaudo.FinalizadoNaoAssinado,
        CancellationToken cancellationToken = default,
        byte[]? carimboAssinaturaSimulado = null)
    {
        var laudo = await _laudos.CarregarParaPdfAsync(laudoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), laudoId);

        // Simulação de PDF assinado (revisão de layout): usa a base limpa de assinatura.
        if (carimboAssinaturaSimulado is not null && laudo.Status == StatusLaudo.Finalizado)
            modo = ModoRodapeLaudo.PreparandoAssinatura;

        // Laudo não finalizado NUNCA é "emitido": rebaixa para Rascunho (marca d'água)
        // qualquer que seja o modo pedido. Só o finalizado distingue
        // "não assinado" (tarja CFM) de "preparando assinatura" (PDF-base limpo).
        var efetivo = laudo.Status != StatusLaudo.Finalizado ? ModoRodapeLaudo.Rascunho : modo;

        var config = await _configuracao.ObterAsync(cancellationToken);
        var blocosCabecalho = ParseHtmlParaBlocos(config.CabecalhoHtml);
        var blocosRodape = ParseHtmlParaBlocos(config.RodapeHtml);
        var temCabecalhoCustom = TemConteudo(config.CabecalhoHtml);
        var temRodapeCustom = TemConteudo(config.RodapeHtml);

        // Resolve as imagens referenciadas no cabeçalho/rodapé (uma vez, antes de renderizar).
        var imagens = await ResolverImagensAsync(
            blocosCabecalho.Concat(blocosRodape), cancellationToken);

        var dadosCabecalho = MontarCabecalhoPaciente(laudo);
        var blocos = ParseHtmlParaBlocos(laudo.ConteudoHtml);
        var emitidoEm = FormatarEmissao(laudo.FinalizadoEm ?? laudo.CriadoEm);

        var documento = QuestDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                page.Header().Element(c => RenderHeader(c, temCabecalhoCustom, blocosCabecalho, imagens));
                page.Content().Element(c => RenderContent(c, laudo, efetivo, dadosCabecalho, blocos, imagens));
                page.Footer().Element(c => RenderFooter(c, efetivo, emitidoEm, temRodapeCustom, blocosRodape, imagens));

                // TEMPORÁRIO (revisão de layout): carimbo da assinatura no MESMO
                // retângulo do Automais.Assinador. Remover junto com a simulação.
                if (carimboAssinaturaSimulado is not null && efetivo == ModoRodapeLaudo.PreparandoAssinatura)
                {
                    page.Foreground().Element(c => RenderCarimboSimulado(c, carimboAssinaturaSimulado));
                }
            });
        });

        using var ms = new MemoryStream();
        documento.GeneratePdf(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// TEMPORÁRIO (revisão de layout): estampa o carimbo no MESMO retângulo do
    /// Automais.Assinador — quadrado de 130pt, centralizado na horizontal, a 28pt
    /// do rodapé. <c>page.Foreground()</c> cobre a página inteira (inclui as
    /// margens), então o posicionamento em pontos casa com o do iText. Remover
    /// quando a simulação de assinatura for desfeita.
    /// </summary>
    private static void RenderCarimboSimulado(IContainer container, byte[] carimboPng)
    {
        container
            .AlignBottom()
            .AlignCenter()
            .PaddingBottom(28)
            .Width(130)
            .Height(130)
            .Image(carimboPng).FitArea();
    }

    // ------------------------ Header ------------------------

    private void RenderHeader(IContainer container, bool custom, IReadOnlyList<BlocoHtml> blocos, IReadOnlyDictionary<string, byte[]> imagens)
    {
        // Cabeçalho configurado no painel tem prioridade; senão, cai no estático do appsettings.
        if (custom)
        {
            container.Column(col =>
            {
                col.Spacing(4);
                foreach (var bloco in blocos)
                {
                    RenderBloco(col.Item(), bloco, imagens);
                }
            });
            return;
        }

        RenderHeaderEstatico(container);
    }

    private void RenderHeaderEstatico(IContainer container)
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

    private void RenderContent(IContainer container, Laudo laudo, ModoRodapeLaudo modo, IEnumerable<(string Rotulo, string Valor)> cabecalhoPaciente, IReadOnlyList<BlocoHtml> blocos, IReadOnlyDictionary<string, byte[]> imagens)
    {
        container.PaddingTop(10).Layers(layers =>
        {
            // Camada primária = conteúdo (define a altura usada pela marca d'água).
            layers.PrimaryLayer().Column(col =>
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

                // Título do laudo
                col.Item().Text(laudo.Titulo).Bold().FontSize(14).AlignCenter();

                // Corpo
                col.Item().Column(corpo =>
                {
                    corpo.Spacing(6);
                    foreach (var bloco in blocos)
                    {
                        RenderBloco(corpo.Item(), bloco, imagens);
                    }
                });
            });

            // Marca d'água diagonal de RASCUNHO: forte e inequívoca, atrás do
            // conteúdo, deixando claro que o documento não tem validade.
            // O Rotate livre do QuestPDF pivota no canto superior-esquerdo; para
            // centralizar, rotacionamos em torno do CENTRO de uma caixa fixa
            // (translada −c, rotaciona, translada +c). A caixa fica ≤ largura útil
            // da A4 (≈482pt) para não estourar "conflicting size constraints".
            if (modo == ModoRodapeLaudo.Rascunho)
            {
                const float w = 460f;
                const float h = 170f;
                layers.Layer()
                    .AlignCenter().AlignMiddle()
                    .Width(w).Height(h)
                    .TranslateX(w / 2).TranslateY(h / 2)
                    .Rotate(-45)
                    .TranslateX(-w / 2).TranslateY(-h / 2)
                    .AlignCenter().AlignMiddle()
                    .Text(t =>
                    {
                        t.AlignCenter();
                        t.Line("RASCUNHO").FontSize(60).Bold().FontColor("#40C62828");
                        t.Line("SEM VALIDADE").FontSize(40).Bold().FontColor("#40C62828");
                    });
            }
        });
    }

    private static void RenderBloco(IContainer container, BlocoHtml bloco, IReadOnlyDictionary<string, byte[]> imagens)
    {
        // Linha (tabela): ocupa a largura toda e renderiza as células lado a lado.
        if (bloco.Tipo == TipoBloco.Linha)
        {
            RenderLinha(container, bloco, imagens);
            return;
        }

        // Alinhamento herdado do style/atributo do HTML.
        container = bloco.Alinhamento switch
        {
            Alinhamento.Centro => container.AlignCenter(),
            Alinhamento.Direita => container.AlignRight(),
            _ => container,
        };

        switch (bloco.Tipo)
        {
            case TipoBloco.Imagem:
                RenderImagem(container, bloco, imagens);
                break;
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

    /// <summary>
    /// Renderiza um bloco Linha (linha de tabela) com as células lado a lado e uma
    /// borda em volta. Célula só-imagem vira coluna de largura fixa (a logo); as
    /// demais ocupam o espaço restante. Vertical centralizado entre as células.
    /// </summary>
    private static void RenderLinha(IContainer container, BlocoHtml bloco, IReadOnlyDictionary<string, byte[]> imagens)
    {
        var celulas = bloco.Celulas;
        if (celulas is null || celulas.Count == 0) return;

        container.Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Row(row =>
        {
            row.Spacing(8);
            foreach (var celula in celulas)
            {
                var soImagem = celula.Count > 0 && celula.All(b => b.Tipo == TipoBloco.Imagem);
                var item = soImagem
                    ? row.ConstantItem(celula.Max(b => b.ImagemLargura ?? 60) + 4)
                    : row.RelativeItem();
                item.AlignMiddle().Column(col =>
                {
                    col.Spacing(2);
                    foreach (var b in celula)
                    {
                        RenderBloco(col.Item(), b, imagens);
                    }
                });
            }
        });
    }

    private static void RenderImagem(IContainer container, BlocoHtml bloco, IReadOnlyDictionary<string, byte[]> imagens)
    {
        if (bloco.ImagemSrc is null || !imagens.TryGetValue(bloco.ImagemSrc, out var bytes))
        {
            return; // imagem não resolvida (externa/ausente) — ignora silenciosamente.
        }

        // Largura útil do A4 (595pt - margens de 2cm ≈ 2*56.7pt) ≈ 482pt.
        const float larguraUtil = 482f;
        if (bloco.ImagemLarguraPct is double pct and >= 0.999)
        {
            // width="100%" → ocupa toda a largura disponível (FitWidth puro, sem
            // largura fixa, para não conflitar com a área útil da página).
            container.Image(bytes).FitWidth();
        }
        else if (bloco.ImagemLarguraPct is double pctParcial and > 0)
        {
            container.Width(larguraUtil * (float)Math.Clamp(pctParcial, 0.05, 1)).Image(bytes).FitWidth();
        }
        else if (bloco.ImagemLargura is int w and > 0)
        {
            // px tratado como pontos; limita à largura útil.
            container.Width(Math.Min(w, larguraUtil)).Image(bytes).FitWidth();
        }
        else
        {
            // Sem dimensão: limita a altura para não explodir o cabeçalho.
            container.MaxHeight(90).Image(bytes).FitHeight();
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

    private void RenderFooter(IContainer container, ModoRodapeLaudo modo, string emitidoEm, bool temRodapeCustom, IReadOnlyList<BlocoHtml> blocosRodape, IReadOnlyDictionary<string, byte[]> imagens)
    {
        container.Column(c =>
        {
            c.Spacing(4);

            // Régua + "Emitido em" SOMEM no PDF-base de assinatura: o carimbo da
            // assinatura ocupa o rodapé central e a data autoritativa é a da própria
            // assinatura digital — manter "Emitido em" aqui só geraria texto escondido
            // sob o carimbo.
            if (modo != ModoRodapeLaudo.PreparandoAssinatura)
            {
                c.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
            }

            // "Emitido em" só no PDF on-demand finalizado e não assinado (sem carimbo
            // por cima). A IDENTIDADE do médico (nome/CRM/RQE + rubrica) NÃO entra aqui
            // em texto solto: vive EXCLUSIVAMENTE no carimbo da assinatura digital.
            if (modo == ModoRodapeLaudo.FinalizadoNaoAssinado)
            {
                c.Item().AlignCenter().Text(emitidoEm).FontSize(8).Light();
            }

            // Tarja neutra "documento sem assinatura digital" só no PDF on-demand
            // (finalizado e não assinado). No PDF-base de assinatura ela some — o
            // documento que será assinado não pode declarar que não está assinado.
            if (modo == ModoRodapeLaudo.FinalizadoNaoAssinado)
            {
                c.Item().PaddingTop(2).AlignCenter().Text(_opt.TarjaRodape)
                    .FontSize(7)
                    .FontColor(Colors.Grey.Darken2)
                    .Italic();
            }

            // Rodapé institucional configurado no painel (endereço/contato/imagem).
            if (temRodapeCustom)
            {
                c.Item().PaddingTop(4).Column(r =>
                {
                    r.Spacing(2);
                    foreach (var bloco in blocosRodape)
                    {
                        RenderBloco(r.Item(), bloco, imagens);
                    }
                });
            }

            // No PDF-base de assinatura o número de página vai p/ a DIREITA, fora do
            // caminho do carimbo (central); nos demais modos, centralizado.
            c.Item()
                .Element(x => modo == ModoRodapeLaudo.PreparandoAssinatura ? x.AlignRight() : x.AlignCenter())
                .Text(t =>
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

    private static bool TemConteudo(string? html) =>
        !string.IsNullOrWhiteSpace(html) && html.Trim() is not ("<p></p>" or "<p><br></p>");

    // ------------------------ Resolução de imagens ------------------------

    private async Task<IReadOnlyDictionary<string, byte[]>> ResolverImagensAsync(
        IEnumerable<BlocoHtml> blocos, CancellationToken cancellationToken)
    {
        var resultado = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var src in Achatar(blocos).Where(b => b.Tipo == TipoBloco.Imagem && b.ImagemSrc is not null)
                                   .Select(b => b.ImagemSrc!)
                                   .Distinct(StringComparer.Ordinal))
        {
            var bytes = await ResolverImagemAsync(src, cancellationToken);
            if (bytes is not null) resultado[src] = bytes;
        }
        return resultado;
    }

    /// <summary>Achata blocos descendo recursivamente nas células de Linha (tabela).</summary>
    private static IEnumerable<BlocoHtml> Achatar(IEnumerable<BlocoHtml> blocos)
    {
        foreach (var b in blocos)
        {
            yield return b;
            if (b.Celulas is null) continue;
            foreach (var celula in b.Celulas)
            {
                foreach (var sub in Achatar(celula))
                {
                    yield return sub;
                }
            }
        }
    }

    private async Task<byte[]?> ResolverImagemAsync(string src, CancellationToken cancellationToken)
    {
        // 1) Data URI (base64 embutido).
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var virgula = src.IndexOf(',');
            if (virgula > 0 && src.Contains(";base64,", StringComparison.OrdinalIgnoreCase))
            {
                try { return Convert.FromBase64String(src[(virgula + 1)..]); }
                catch { return null; }
            }
            return null;
        }

        // 2) Mídia local (/midias/{guid}).
        var idx = src.IndexOf("/midias/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var resto = src[(idx + "/midias/".Length)..];
            var fim = resto.IndexOfAny(['/', '?', '#']);
            if (fim >= 0) resto = resto[..fim];
            if (Guid.TryParse(resto, out var id))
            {
                var conteudo = await _midias.ObterConteudoAsync(id, cancellationToken);
                return conteudo?.Conteudo;
            }
        }

        return null; // URL externa — não embute em PDF offline.
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
            ColetarBlocos(filho, blocos, null, Alinhamento.Esquerda);
        }
        if (blocos.Count == 0)
        {
            blocos.Add(new BlocoHtml(TipoBloco.Paragrafo, [new SpanInline(html, false, false, false)], null));
        }
        return blocos;
    }

    private static void ColetarBlocos(DomNode node, List<BlocoHtml> destino, string? prefixoLista, Alinhamento alinhamentoHerdado)
    {
        if (node is DomText txt && !string.IsNullOrWhiteSpace(txt.TextContent))
        {
            destino.Add(new BlocoHtml(TipoBloco.Paragrafo,
                [new SpanInline(txt.TextContent, false, false, false)],
                prefixoLista, alinhamentoHerdado));
            return;
        }

        if (node is not DomElement el) return;

        var tag = el.TagName.ToUpperInvariant();
        var alinhamento = LerAlinhamento(el, alinhamentoHerdado);

        switch (tag)
        {
            case "IMG":
                destino.Add(BlocoImagem(el, alinhamento));
                break;
            case "P":
                AdicionarComImagensInline(el, destino, TipoBloco.Paragrafo, prefixoLista, alinhamento);
                break;
            case "H1":
                destino.Add(new BlocoHtml(TipoBloco.Titulo1, ColetarSpans(el), null, alinhamento));
                break;
            case "H2":
                destino.Add(new BlocoHtml(TipoBloco.Titulo2, ColetarSpans(el), null, alinhamento));
                break;
            case "H3":
            case "H4":
            case "H5":
            case "H6":
                destino.Add(new BlocoHtml(TipoBloco.Titulo3, ColetarSpans(el), null, alinhamento));
                break;
            case "UL":
                foreach (var li in el.Children.Where(c => string.Equals(c.TagName, "LI", StringComparison.OrdinalIgnoreCase)))
                {
                    destino.Add(new BlocoHtml(TipoBloco.ItemLista, ColetarSpans(li), "•", alinhamento));
                }
                break;
            case "OL":
                var i = 1;
                foreach (var li in el.Children.Where(c => string.Equals(c.TagName, "LI", StringComparison.OrdinalIgnoreCase)))
                {
                    destino.Add(new BlocoHtml(TipoBloco.ItemLista, ColetarSpans(li), $"{i}.", alinhamento));
                    i++;
                }
                break;
            case "BR":
                destino.Add(new BlocoHtml(TipoBloco.Paragrafo, [new SpanInline(string.Empty, false, false, false)], null, alinhamento));
                break;
            case "TABLE":
                // Cada <tr> vira um bloco Linha cujas células (td/th) são listas de
                // blocos renderizadas lado a lado. Usado p/ cabeçalho logo|texto|logo.
                foreach (var tr in el.QuerySelectorAll("tr"))
                {
                    var celulas = new List<IReadOnlyList<BlocoHtml>>();
                    foreach (var td in tr.Children.Where(c =>
                        string.Equals(c.TagName, "TD", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c.TagName, "TH", StringComparison.OrdinalIgnoreCase)))
                    {
                        var sub = new List<BlocoHtml>();
                        var alinhCel = LerAlinhamento(td, alinhamento);
                        foreach (var filho in td.ChildNodes)
                        {
                            ColetarBlocos(filho, sub, null, alinhCel);
                        }
                        celulas.Add(sub);
                    }
                    if (celulas.Count > 0)
                    {
                        destino.Add(new BlocoHtml(TipoBloco.Linha, [], null, alinhamento, Celulas: celulas));
                    }
                }
                break;
            default:
                // Container desconhecido (div, span, section, etc): desce recursivamente.
                foreach (var filho in el.ChildNodes)
                {
                    ColetarBlocos(filho, destino, prefixoLista, alinhamento);
                }
                break;
        }
    }

    /// <summary>
    /// Um &lt;p&gt; pode conter texto e/ou uma imagem (TipTap às vezes aninha
    /// &lt;img&gt; em parágrafo). Emite a imagem como bloco próprio e o resto como texto.
    /// </summary>
    private static void AdicionarComImagensInline(DomElement el, List<BlocoHtml> destino, TipoBloco tipo, string? prefixoLista, Alinhamento alinhamento)
    {
        var imgs = el.Children.Where(c => string.Equals(c.TagName, "IMG", StringComparison.OrdinalIgnoreCase)).ToList();
        var spans = ColetarSpans(el);

        if (spans.Count > 0 || imgs.Count == 0)
        {
            destino.Add(new BlocoHtml(tipo, spans, prefixoLista, alinhamento));
        }
        foreach (var img in imgs)
        {
            destino.Add(BlocoImagem(img, alinhamento));
        }
    }

    private static BlocoHtml BlocoImagem(DomElement img, Alinhamento alinhamento)
    {
        var src = img.GetAttribute("src");
        var larguraAttr = img.GetAttribute("width");
        int? largura = int.TryParse(larguraAttr, out var w) ? w : null;
        double? larguraPct = null;
        if (largura is null && larguraAttr is not null && larguraAttr.EndsWith('%')
            && double.TryParse(larguraAttr.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture, out var pct))
        {
            larguraPct = pct / 100.0;
        }
        int? altura = int.TryParse(img.GetAttribute("height"), out var h) ? h : null;
        return new BlocoHtml(TipoBloco.Imagem, [], null, alinhamento, src, largura, altura, larguraPct);
    }

    private static Alinhamento LerAlinhamento(DomElement el, Alinhamento herdado)
    {
        var style = el.GetAttribute("style");
        if (!string.IsNullOrEmpty(style) && style.Contains("text-align", StringComparison.OrdinalIgnoreCase))
        {
            if (style.Contains("center", StringComparison.OrdinalIgnoreCase)) return Alinhamento.Centro;
            if (style.Contains("right", StringComparison.OrdinalIgnoreCase)) return Alinhamento.Direita;
            if (style.Contains("left", StringComparison.OrdinalIgnoreCase)) return Alinhamento.Esquerda;
        }
        return herdado;
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
                if (tag == "IMG") continue; // imagem vira bloco próprio, não span.
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

    private enum TipoBloco { Paragrafo, Titulo1, Titulo2, Titulo3, ItemLista, Imagem, Linha }

    private enum Alinhamento { Esquerda, Centro, Direita }

    private sealed record SpanInline(string Texto, bool Bold, bool Italic, bool Underline);

    private sealed record BlocoHtml(
        TipoBloco Tipo,
        IReadOnlyList<SpanInline> Spans,
        string? PrefixoLista,
        Alinhamento Alinhamento = Alinhamento.Esquerda,
        string? ImagemSrc = null,
        int? ImagemLargura = null,
        int? ImagemAltura = null,
        // Largura em fração (0..1) quando o HTML usa width="NN%" — relativa à
        // largura útil da página (full-width quando 100%).
        double? ImagemLarguraPct = null,
        // Para TipoBloco.Linha: cada célula é uma lista de blocos (linha de tabela
        // renderizada lado a lado — usado p/ cabeçalho logo|texto|logo).
        IReadOnlyList<IReadOnlyList<BlocoHtml>>? Celulas = null);
}
