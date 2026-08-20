using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.Midias;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Core.Worklist;
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
    IPacientesService pacientes,
    ISolicitacoesExameService solicitacoes,
    IConsultaStudyClient consultaStudy,
    IOptions<LaudosPdfOptions> options) : ILaudoPdfRenderer
{
    private readonly ILaudosService _laudos = laudos;
    private readonly ILaudoConfiguracaoService _configuracao = configuracao;
    private readonly IMidiasService _midias = midias;
    private readonly IPacientesService _pacientes = pacientes;
    private readonly ISolicitacoesExameService _solicitacoes = solicitacoes;
    private readonly IConsultaStudyClient _consultaStudy = consultaStudy;
    private readonly LaudosPdfOptions _opt = options.Value;

    /// <summary>
    /// Altura reservada ao carimbo da assinatura no fim do conteúdo (modo
    /// PreparandoAssinatura). Topo do carimbo = 158pt do pé da página; margem
    /// inferior de 2cm ≈ 57pt e rodapé mínimo (nº da página) ≈ 14pt → o carimbo
    /// invade ≈ 87pt da área útil. 100pt cobre com folga rodapés enxutos.
    /// </summary>
    private const float ReservaCarimboPt = 100f;

    /// <summary>
    /// Corpo do laudo é 10pt; dentro de tabela de dados cai para 9pt — uma tabela
    /// de 7 colunas (densitometria) não cabe na largura útil da A4 com 10pt sem
    /// quebrar os títulos em 3 linhas.
    /// </summary>
    private const float FonteTabela = 9f;

    /// <summary>Classe que marca uma tabela como "de dados" (grade, cabeçalho, larguras).</summary>
    private const string ClasseTabelaDados = "laudo-tabela";

    public async Task<byte[]> GerarAsync(
        Guid laudoId,
        ModoRodapeLaudo modo = ModoRodapeLaudo.FinalizadoNaoAssinado,
        CancellationToken cancellationToken = default)
    {
        var laudo = await _laudos.CarregarParaPdfAsync(laudoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Laudo), laudoId);

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

        var paciente = await ResolverPacienteAsync(laudo.PacienteId, cancellationToken);
        var solicitacao = await ResolverSolicitacaoAsync(laudo.StudyInstanceUID, cancellationToken);
        var dataExame = await ResolverDataExameAsync(solicitacao, laudo.StudyInstanceUID, cancellationToken);
        var dadosCabecalho = MontarCabecalhoPaciente(laudo, paciente, solicitacao, dataExame);
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
            });
        });

        using var ms = new MemoryStream();
        documento.GeneratePdf(ms);
        return ms.ToArray();
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

    private void RenderContent(IContainer container, Laudo laudo, ModoRodapeLaudo modo, IEnumerable<IReadOnlyList<(string Rotulo, string Valor)>> cabecalhoPaciente, IReadOnlyList<BlocoHtml> blocos, IReadOnlyDictionary<string, byte[]> imagens)
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
                    // Dados do paciente — fonte um pouco menor que o corpo (10).
                    foreach (var linha in cabecalhoPaciente)
                    {
                        p.Item().DefaultTextStyle(t => t.FontSize(9)).Text(span =>
                        {
                            var primeiro = true;
                            foreach (var (rotulo, valor) in linha)
                            {
                                if (!primeiro) span.Span("      ");
                                span.Span($"{rotulo}: ").SemiBold();
                                span.Span(valor);
                                primeiro = false;
                            }
                        });
                    }
                    // Study Instance UID — dado técnico: menor ainda e em cinza.
                    p.Item().DefaultTextStyle(t => t.FontSize(7).FontColor(Colors.Grey.Darken1)).Text(span =>
                    {
                        span.Span("Study Instance UID: ").SemiBold();
                        span.Span(laudo.StudyInstanceUID);
                    });
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

                // RESERVA DO CARIMBO (só no PDF-base de assinatura): o Automais.Assinador
                // estampa um quadrado de 130pt a 28..158pt do pé da ÚLTIMA página. Com a
                // margem de 2cm (~57pt) + rodapé, o carimbo invade ~100pt da área útil —
                // este bloco vazio e inquebrável garante que o TEXTO nunca termine dentro
                // dessa zona: se não couber, o QuestPDF quebra a página e o carimbo cai
                // numa página limpa. (Correção do carimbo sobreposto ao texto.)
                if (modo == ModoRodapeLaudo.PreparandoAssinatura)
                {
                    col.Item().Height(ReservaCarimboPt);
                }
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

    private static void RenderBloco(IContainer container, BlocoHtml bloco, IReadOnlyDictionary<string, byte[]> imagens,
        float fonteBase = 10f)
    {
        // Linha (tabela): ocupa a largura toda e renderiza as células lado a lado.
        if (bloco.Tipo == TipoBloco.Linha)
        {
            RenderLinha(container, bloco, imagens);
            return;
        }

        // Tabela de dados (<table class="laudo-tabela">): grade contínua de verdade.
        if (bloco.Tipo == TipoBloco.Tabela)
        {
            RenderTabela(container, bloco, imagens);
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
                container.Text(t => RenderInline(t, bloco.Spans, baseSize: fonteBase, baseBold: false));
                break;
            case TipoBloco.ItemLista:
                container.Row(r =>
                {
                    r.ConstantItem(14).Text(bloco.PrefixoLista ?? "•").FontSize(fonteBase);
                    r.RelativeItem().Text(t => RenderInline(t, bloco.Spans, baseSize: fonteBase, baseBold: false));
                });
                break;
        }
    }

    /// <summary>
    /// Renderiza <c>&lt;table class="laudo-tabela"&gt;</c> como tabela de verdade:
    /// grade contínua, larguras de coluna vindas do HTML, cabeçalho destacado e
    /// repetido quando a tabela quebra de página.
    ///
    /// <para>
    /// É um caminho <b>opt-in</b>: tabelas sem a classe continuam em
    /// <see cref="RenderLinha"/> (uma caixa por &lt;tr&gt;), que é o layout usado
    /// pelo cabeçalho institucional configurado no painel — este NÃO muda.
    /// </para>
    /// </summary>
    private static void RenderTabela(IContainer container, BlocoHtml bloco, IReadOnlyDictionary<string, byte[]> imagens)
    {
        var linhas = bloco.Linhas;
        if (linhas is null || linhas.Count == 0) return;

        // Nº de colunas = maior soma de colspan entre as linhas.
        var colunas = linhas.Max(l => l.Celulas.Sum(c => Math.Max(1, c.ColSpan)));
        if (colunas <= 0) return;

        var pesos = PesosColunas(bloco.ColunasLargura, colunas);
        var cabecalho = linhas.FirstOrDefault(l => l.EhCabecalho);
        var corpo = linhas.Where(l => !ReferenceEquals(l, cabecalho)).ToList();

        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(cols =>
            {
                foreach (var peso in pesos) cols.RelativeColumn(peso);
            });

            if (cabecalho is not null)
            {
                tabela.Header(h => MontarLinha(
                    (cs, rs) => h.Cell().ColumnSpan(cs).RowSpan(rs),
                    cabecalho, colunas, imagens, ehCabecalho: true));
            }

            foreach (var linha in corpo)
            {
                MontarLinha(
                    (cs, rs) => tabela.Cell().ColumnSpan(cs).RowSpan(rs),
                    linha, colunas, imagens, ehCabecalho: false);
            }
        });
    }

    /// <summary>
    /// Emite as células de uma linha e COMPLETA a linha com células vazias até
    /// fechar o nº de colunas.
    ///
    /// <para>
    /// O preenchimento não é cosmético: o QuestPDF posiciona célula a célula em
    /// sequência, então uma linha curta (HTML editado na mão, célula apagada)
    /// puxaria a 1ª célula da linha seguinte para o buraco e deslocaria a tabela
    /// inteira dali para baixo — sem erro nenhum.
    /// </para>
    /// </summary>
    private static void MontarLinha(Func<uint, uint, IContainer> novaCelula, LinhaTabela linha,
        int colunas, IReadOnlyDictionary<string, byte[]> imagens, bool ehCabecalho)
    {
        var ocupadas = 0;
        foreach (var celula in linha.Celulas)
        {
            var span = Math.Max(1, celula.ColSpan);
            if (ocupadas + span > colunas) break; // linha malformada: não estoura a tabela.

            MontarCelula(
                novaCelula((uint)span, (uint)Math.Max(1, celula.RowSpan)),
                celula, imagens, ehCabecalho);
            ocupadas += span;
        }

        for (; ocupadas < colunas; ocupadas++)
        {
            MontarCelula(novaCelula(1, 1), new CelulaTabela([], 1, 1, null), imagens, ehCabecalho);
        }
    }

    /// <summary>Aplica moldura/fundo e despeja os blocos dentro de uma célula.</summary>
    private static void MontarCelula(IContainer cell, CelulaTabela celula,
        IReadOnlyDictionary<string, byte[]> imagens, bool ehCabecalho)
    {
        var alvo = cell.Border(0.5f).BorderColor(Colors.Grey.Darken1);

        if (ehCabecalho) alvo = alvo.Background(Colors.Grey.Lighten3);

        alvo.PaddingVertical(3).PaddingHorizontal(4).AlignMiddle().Column(col =>
        {
            col.Spacing(1);
            if (celula.Blocos.Count == 0)
            {
                // Célula vazia ainda precisa ocupar altura para a grade fechar.
                col.Item().Text(string.Empty).FontSize(FonteTabela);
                return;
            }
            foreach (var b in celula.Blocos)
            {
                // Cabeçalho sai em negrito, marcando os spans (o <th> do HTML não
                // carrega <strong>, o destaque é semântico da tag).
                var bloco = ehCabecalho && b.Tipo == TipoBloco.Paragrafo
                    ? b with { Spans = [.. b.Spans.Select(s => s with { Bold = true })] }
                    : b;
                RenderBloco(col.Item(), bloco, imagens, FonteTabela);
            }
        });
    }

    /// <summary>
    /// Pesos relativos das colunas. Usa <c>style="width:NN%"</c> / <c>colwidth</c>
    /// das células da 1ª linha; sem isso, colunas iguais.
    /// </summary>
    private static IReadOnlyList<float> PesosColunas(IReadOnlyList<float?>? larguras, int colunas)
    {
        var pesos = new float[colunas];
        for (var i = 0; i < colunas; i++)
        {
            var l = larguras is not null && i < larguras.Count ? larguras[i] : null;
            pesos[i] = l is > 0 ? l.Value : 0f;
        }

        // Colunas sem largura declarada recebem a média das declaradas (ou 1).
        var declaradas = pesos.Where(p => p > 0).ToList();
        var padrao = declaradas.Count > 0 ? declaradas.Average() : 1f;
        for (var i = 0; i < colunas; i++)
        {
            if (pesos[i] <= 0) pesos[i] = padrao;
        }
        return pesos;
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

    /// <summary>Resolve os dados do paciente no hub FHIR; null se sem vínculo ou não encontrado.</summary>
    private async Task<PacienteDto?> ResolverPacienteAsync(Guid? pacienteId, CancellationToken ct)
    {
        if (pacienteId is null) return null;
        try
        {
            return await _pacientes.ObterPorIdAsync(pacienteId.Value, ct);
        }
        catch (NaoEncontradoException)
        {
            return null;
        }
    }

    /// <summary>
    /// Solicitação ligada ao estudo (match direto por StudyInstanceUID ou via associação).
    /// Falha na resolução NUNCA derruba o PDF — só omite os dados do pedido.
    /// </summary>
    private async Task<SolicitacaoExameDto?> ResolverSolicitacaoAsync(string studyInstanceUID, CancellationToken ct)
    {
        try
        {
            return await _solicitacoes.ObterPorStudyAsync(studyInstanceUID, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Data/hora do exame para o cabeçalho, já formatada e no fuso de exibição. Fonte da
    /// verdade: <c>DataEstudo</c> (DICOM StudyDate/StudyTime); se ausente, cai para
    /// <c>RealizadoEm</c> (hora de detecção pelo servidor, UTC→local); em último caso,
    /// consulta o PACS ao vivo. Null quando nada disso está disponível — a linha é omitida.
    /// NUNCA usa a data da SOLICITAÇÃO como data do exame.
    /// </summary>
    private async Task<string?> ResolverDataExameAsync(
        SolicitacaoExameDto? solicitacao, string studyInstanceUID, CancellationToken ct)
    {
        // 1) DICOM persistido (wall-clock local) — exibe como está.
        if (solicitacao?.DataEstudo is { } dicom)
            return dicom.ToString("dd/MM/yyyy HH:mm");

        // 2) Hora de detecção pelo servidor (UTC) — converte para o fuso de exibição.
        if (solicitacao?.RealizadoEm is { } realizado)
            return realizado.AddHours(_opt.OffsetHorasParaExibicao).ToString("dd/MM/yyyy HH:mm");

        // 3) Último recurso: consulta o PACS ao vivo (StudyDate/StudyTime), blindado.
        try
        {
            if (await _consultaStudy.ObterDataHoraEstudoAsync(studyInstanceUID, ct) is { } aoVivo)
                return aoVivo.ToString("dd/MM/yyyy HH:mm");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PACS indisponível — omite a linha (nunca derruba o PDF).
        }
        return null;
    }

    // Cada item é uma LINHA do cabeçalho (1+ campos rótulo/valor renderizados lado a lado).
    private static IReadOnlyList<IReadOnlyList<(string Rotulo, string Valor)>> MontarCabecalhoPaciente(
        Laudo l, PacienteDto? p, SolicitacaoExameDto? s, string? dataExame)
    {
        var linhas = new List<IReadOnlyList<(string, string)>>();

        if (p is not null)
        {
            var nome = string.IsNullOrWhiteSpace(p.NomeSocial)
                ? p.NomeCompleto
                : $"{p.NomeSocial} ({p.NomeCompleto})";
            linhas.Add([("Paciente", nome)]);

            // CPF + CNS na mesma linha (CNS só quando houver).
            var docs = new List<(string, string)>();
            if (!string.IsNullOrWhiteSpace(p.Cpf)) docs.Add(("CPF", FormatarCpf(p.Cpf)));
            if (!string.IsNullOrWhiteSpace(p.Cns)) docs.Add(("CNS", p.Cns!));
            if (docs.Count > 0) linhas.Add(docs);

            // Nascimento + Sexo na mesma linha.
            var bio = new List<(string, string)>();
            if (p.DataNascimento is { } nasc) bio.Add(("Nascimento", nasc.ToString("dd/MM/yyyy")));
            bio.Add(("Sexo", DescreverSexo(p.Sexo)));
            linhas.Add(bio);
        }
        else
        {
            // Sem paciente resolvido — nunca imprime o UUID cru no documento.
            linhas.Add([("Paciente", l.PacienteId.HasValue ? "Não encontrado" : "Não vinculado")]);
        }

        // Data REAL do exame (DICOM StudyDate/StudyTime) — jamais a data da solicitação.
        if (!string.IsNullOrWhiteSpace(dataExame))
            linhas.Add([("Data do exame", dataExame!)]);

        // Dados do pedido/exame (quando há solicitação ligada ao estudo).
        if (s is not null)
        {
            linhas.Add([("Unidade executora", s.UnidadeNome)]);

            if (!string.IsNullOrWhiteSpace(s.UnidadeSolicitanteNome))
                linhas.Add([("Unidade solicitante", s.UnidadeSolicitanteNome!)]);

            // Solicitante = só o nome (CRM/COREN saiu do produto).
            if (!string.IsNullOrWhiteSpace(s.SolicitanteNome))
                linhas.Add([("Solicitante", s.SolicitanteNome)]);

            if (!string.IsNullOrWhiteSpace(s.CodigoSolicitacao))
                linhas.Add([("Código da Solicitação", s.CodigoSolicitacao!)]);
        }

        // O Study Instance UID (dado técnico) é renderizado à parte, em fonte menor.
        return linhas;
    }

    private static string DescreverSexo(Sexo sexo) => sexo switch
    {
        Sexo.Masculino => "Masculino",
        Sexo.Feminino => "Feminino",
        Sexo.Outro => "Outro",
        _ => "Não informado",
    };

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

    /// <summary>Achata blocos descendo recursivamente nas células de Linha e de Tabela.</summary>
    private static IEnumerable<BlocoHtml> Achatar(IEnumerable<BlocoHtml> blocos)
    {
        foreach (var b in blocos)
        {
            yield return b;

            if (b.Celulas is not null)
            {
                foreach (var celula in b.Celulas)
                {
                    foreach (var sub in Achatar(celula)) yield return sub;
                }
            }

            if (b.Linhas is not null)
            {
                foreach (var linha in b.Linhas)
                {
                    foreach (var celula in linha.Celulas)
                    {
                        foreach (var sub in Achatar(celula.Blocos)) yield return sub;
                    }
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
                // Tabela de DADOS (opt-in pela classe): vira um único bloco Tabela com
                // grade contínua, cabeçalho e larguras de coluna.
                if (el.ClassList.Contains(ClasseTabelaDados))
                {
                    var tabela = ColetarTabela(el, alinhamento);
                    if (tabela is not null) destino.Add(tabela);
                    break;
                }

                // Caminho legado (tabela de LAYOUT): cada <tr> vira um bloco Linha cujas
                // células (td/th) são listas de blocos renderizadas lado a lado. É o que
                // o cabeçalho institucional (logo|texto|logo) usa — não mexer.
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
    /// Lê uma tabela de dados inteira (<c>class="laudo-tabela"</c>) para um único
    /// bloco. A linha de cabeçalho é a primeira que estiver dentro de
    /// <c>&lt;thead&gt;</c> ou que seja composta só de <c>&lt;th&gt;</c>.
    /// </summary>
    private static BlocoHtml? ColetarTabela(DomElement el, Alinhamento alinhamento)
    {
        var linhas = new List<LinhaTabela>();

        foreach (var tr in el.QuerySelectorAll("tr"))
        {
            var celulas = new List<CelulaTabela>();
            var todasTh = true;

            foreach (var td in tr.Children.Where(c =>
                string.Equals(c.TagName, "TD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.TagName, "TH", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(td.TagName, "TH", StringComparison.OrdinalIgnoreCase)) todasTh = false;

                var sub = new List<BlocoHtml>();
                var alinhCel = LerAlinhamento(td, alinhamento);
                foreach (var filho in td.ChildNodes)
                {
                    ColetarBlocos(filho, sub, null, alinhCel);
                }

                celulas.Add(new CelulaTabela(
                    sub,
                    LerSpan(td, "colspan"),
                    LerSpan(td, "rowspan"),
                    LerLarguraCelula(td)));
            }

            if (celulas.Count == 0) continue;

            var noThead = tr.ParentElement is { } pai
                && string.Equals(pai.TagName, "THEAD", StringComparison.OrdinalIgnoreCase);

            linhas.Add(new LinhaTabela(noThead || (todasTh && linhas.Count == 0), celulas));
        }

        if (linhas.Count == 0) return null;

        // Larguras: a 1ª linha manda (é onde o TipTap grava o colwidth).
        var larguras = linhas[0].Celulas.Select(c => c.Largura).ToList();

        return new BlocoHtml(TipoBloco.Tabela, [], null, alinhamento,
            Linhas: linhas, ColunasLargura: larguras);
    }

    private static int LerSpan(DomElement el, string atributo) =>
        int.TryParse(el.GetAttribute(atributo), out var n) && n > 0 ? n : 1;

    /// <summary>
    /// Largura declarada de uma célula, em unidade arbitrária (só a proporção
    /// importa): <c>style="width:NN%"</c>, <c>style="width:NNpx"</c> ou o
    /// <c>colwidth</c> que o TipTap grava ao redimensionar a coluna.
    /// </summary>
    private static float? LerLarguraCelula(DomElement el)
    {
        var style = el.GetAttribute("style");
        if (!string.IsNullOrWhiteSpace(style))
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                style, @"width\s*:\s*([\d.,]+)\s*(%|px|pt)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && float.TryParse(m.Groups[1].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var w) && w > 0)
            {
                return w;
            }
        }

        // colwidth do TipTap pode vir como lista ("120,80") quando a célula tem colspan.
        var colwidth = el.GetAttribute("colwidth");
        if (!string.IsNullOrWhiteSpace(colwidth))
        {
            var soma = 0f;
            foreach (var parte in colwidth.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (float.TryParse(parte.Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v)) soma += v;
            }
            if (soma > 0) return soma;
        }

        return null;
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

    private enum TipoBloco { Paragrafo, Titulo1, Titulo2, Titulo3, ItemLista, Imagem, Linha, Tabela }

    private enum Alinhamento { Esquerda, Centro, Direita }

    private sealed record SpanInline(string Texto, bool Bold, bool Italic, bool Underline);

    /// <summary>Célula de uma tabela de dados (TipoBloco.Tabela).</summary>
    private sealed record CelulaTabela(
        IReadOnlyList<BlocoHtml> Blocos,
        int ColSpan,
        int RowSpan,
        float? Largura);

    /// <summary>Linha de uma tabela de dados; a de cabeçalho repete a cada página.</summary>
    private sealed record LinhaTabela(bool EhCabecalho, IReadOnlyList<CelulaTabela> Celulas);

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
        IReadOnlyList<IReadOnlyList<BlocoHtml>>? Celulas = null,
        // Para TipoBloco.Tabela: a tabela inteira num bloco só.
        IReadOnlyList<LinhaTabela>? Linhas = null,
        IReadOnlyList<float?>? ColunasLargura = null);
}
