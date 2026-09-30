using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMais.Core.Exames;
using SMSMais.Core.Regulacao.Indicadores.Dtos;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>
/// PDF dos Indicadores de Regulação de um sistema: A4 paisagem, identidade visual da instituição,
/// uma seção por indicador com gráfico (SVG), tabela mês a mês em blocos por ano e o selo de origem em
/// cada linha. Mesmo desenho do relatório de 30/09/2026 (docs/regulacao/relatorio-2025-2026/montar.py).
/// </summary>
internal sealed class IndicadoresRegulacaoPdf(IndicadoresRegulacaoDto dados, IdentidadeVisualPdf idv)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] MesesAbr = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    // Paleta categórica validada (dataviz); a cor da marca fica só no "cromo" (títulos, régua).
    private static readonly string[] Cores = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100"];
    private static readonly string[] MotivosNoFim = ["Outros", "Sem motivo informado"];
    private const string Tinta = "#1f2328";
    private const string Suave = "#57606a";
    private const string Linha = "#d0d7de";

    public byte[] Gerar() => QuestDocument.Create(Compor).GeneratePdf();

    private void Compor(IDocumentContainer doc)
    {
        doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginHorizontal(1.3f, Unit.Centimetre);
            page.MarginVertical(1.1f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Helvetica").FontColor(Tinta));
            page.Header().Element(Cabecalho);
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(14);
                col.Item().Element(Abertura);
                foreach (var s in dados.Secoes) col.Item().Element(c => Secao(c, s));
                col.Item().Element(Metodologia);
            });
            page.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Gerado em {FusoHorario(dados.GeradoEm):dd/MM/yyyy HH:mm} · fonte: {dados.NomeSistema}")
                    .FontSize(7).FontColor(Suave);
                r.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Suave));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });
    }

    private void Cabecalho(IContainer c) => c.Column(col =>
    {
        col.Item().Row(row =>
        {
            if (idv.Logo is not null) row.ConstantItem(120).Height(34).AlignLeft().Image(idv.Logo).FitArea();
            row.RelativeItem().AlignRight().AlignMiddle().Column(t =>
            {
                t.Item().AlignRight().Text(idv.Nome).FontSize(10).Bold().FontColor(idv.CorPrimaria);
                t.Item().AlignRight().Text($"Indicadores de Regulação — {dados.Sistema} · {Periodo()}").FontSize(8).FontColor(Suave);
            });
        });
        col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(idv.CorPrimaria);
    });

    private void Abertura(IContainer c) => c.Column(col =>
    {
        col.Item().Text("Indicadores de Regulação").FontSize(20).Bold().FontColor(idv.CorEscura);
        col.Item().Text(dados.NomeSistema).FontSize(12).FontColor(Tinta);
        col.Item().PaddingTop(2).Text($"Período: {Periodo()} · série mensal, agrupada por ano").FontSize(9).FontColor(Suave);
        if (dados.Cobertura.Count > 0)
            col.Item().PaddingTop(8).Background("#f6f8fa").Border(0.5f).BorderColor(Linha).Padding(8).Column(cob =>
            {
                cob.Spacing(2);
                cob.Item().Text("Cobertura dos dados").Bold().FontSize(8.5f);
                foreach (var linha in dados.Cobertura) cob.Item().Text($"• {linha}").FontSize(8).FontColor(Suave);
            });
        col.Item().PaddingTop(6).Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Suave));
            t.Span("Cada número traz o selo de origem: ");
            t.Span("Oficial").Bold(); t.Span(" (lido do sistema), ");
            t.Span("Calculado").Bold(); t.Span(" (derivado de dados oficiais), ");
            t.Span("Parcial").Bold(); t.Span(" (piso — sabidamente incompleto), ");
            t.Span("Indisponível").Bold(); t.Span(" (o sistema não fornece). Metodologia ao final.");
        });
    });

    private void Secao(IContainer c, SecaoIndicadorDto s) => c.Column(col =>
    {
        col.Spacing(5);
        col.Item().EnsureSpace(160).Column(cab =>
        {
            cab.Item().Text(s.Titulo).FontSize(12.5f).Bold().FontColor(idv.CorEscura);
            cab.Item().PaddingTop(1).LineHorizontal(0.5f).LineColor(Linha);
            if (!string.IsNullOrWhiteSpace(s.Texto))
                cab.Item().PaddingTop(3).Text(s.Texto!).FontSize(8).FontColor(Suave);
        });
        if (s.Indisponivel)
        {
            col.Item().Background("#f6f8fa").Border(0.5f).BorderColor(Linha).Padding(6)
                .Text("Indisponível — o sistema de origem não fornece este dado ao município.").FontSize(8.5f).Italic().FontColor(Suave);
            return;
        }

        if (Grafico(s) is { } svg)
            col.Item().ShowEntire().Column(g =>
            {
                g.Item().Svg(svg.Svg).FitWidth();
                if (svg.Legenda.Count > 1)
                    g.Item().PaddingTop(2).Inlined(r =>
                    {
                        r.Spacing(12);
                        for (var i = 0; i < svg.Legenda.Count; i++)
                        {
                            var cor = Cores[i % Cores.Length];
                            r.Item().Row(l =>
                            {
                                l.ConstantItem(8).Height(8).Background(cor);
                                l.AutoItem().PaddingLeft(3).Text(svg.Legenda[i]).FontSize(7.5f).FontColor(Suave);
                            });
                        }
                    });
            });

        if (s.Series.Count > 0)
            foreach (var bloco in Anos())
                col.Item().EnsureSpace(90).Element(x => TabelaSerie(x, s, bloco.Ano, bloco.Meses));

        foreach (var nota in s.Series.Where(x => !string.IsNullOrWhiteSpace(x.Nota)))
            col.Item().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7).FontColor(Suave));
                t.Span($"{nota.Rotulo}: ").SemiBold();
                t.Span(nota.Nota!);
            });

        if (s.Motivos is { } motivos) col.Item().Element(x => Tabela(x, Motivos(motivos), true));
        foreach (var t in s.Tabelas.Where(t => t.Linhas.Count > 0)) col.Item().Element(x => Tabela(x, t, false));

        if (s.ResumoJudicial is { N: > 0 } r)
            col.Item().Text($"Tempo até o agendamento das judicializadas do período ({r.N} com agendamento): mediana {Dias(r.Mediana)} · P90 {Dias(r.P90)} · menor {Dias(r.Menor)} · maior {Dias(r.Maior)} dias.")
                .FontSize(8).FontColor(Tinta);
    });

    // ------------------------------------------------------------------------------------ tabelas
    private void TabelaSerie(IContainer c, SecaoIndicadorDto s, string ano, IReadOnlyList<string> meses) =>
        c.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(5.2f);
                d.ConstantColumn(58);
                foreach (var _ in meses) d.RelativeColumn(1);
                d.RelativeColumn(1.35f);
            });
            t.Header(h =>
            {
                h.Cell().Element(CabCelula).Text(RotuloAno(ano, meses)).Bold();
                h.Cell().Element(CabCelula).Text("Origem");
                foreach (var m in meses) h.Cell().Element(CabCelula).AlignRight().Text(MesesAbr[int.Parse(m[5..], CultureInfo.InvariantCulture) - 1]);
                h.Cell().Element(CabCelula).AlignRight().Text("No período");
            });
            foreach (var serie in s.Series)
            {
                var (tot, tipo) = Agregado(serie, ano, meses);
                IContainer Cel(IContainer x) => x.BorderBottom(0.4f).BorderColor(Linha).PaddingVertical(2.2f).PaddingHorizontal(2)
                    .Background(serie.Destaque ? "#f6f8fa" : Colors.White);
                t.Cell().Element(Cel).PaddingLeft(serie.Subitem ? 10 : 0).Text(serie.Rotulo)
                    .FontSize(serie.Subitem ? 7.5f : 8).FontColor(serie.Subitem ? Suave : Tinta).Style(TextStyle.Default.Weight(serie.Destaque ? FontWeight.SemiBold : FontWeight.Normal));
                t.Cell().Element(Cel).Element(x => Selo(x, serie.Selo));
                foreach (var m in meses)
                    t.Cell().Element(Cel).AlignRight().Text(Valor(serie.Valores.GetValueOrDefault(m), serie.Formato)).FontSize(7.8f);
                t.Cell().Element(Cel).AlignRight().Text(x =>
                {
                    x.Span(Valor(tot, serie.Formato)).Bold().FontSize(7.8f);
                    if (tipo.Length > 0) x.Span($" {tipo}").FontSize(6).FontColor(Suave);
                });
            }
        });

    /// <summary>Tabela genérica: largura de cada coluna pelo conteúdo; coluna só de números alinha à direita.</summary>
    private static void Tabela(IContainer c, TabelaIndicadorDto tab, bool motivos) => c.EnsureSpace(90).Column(col =>
    {
        var n = tab.Colunas.Count;
        string Cel(IReadOnlyList<string?> l, int i) => i < l.Count ? l[i] ?? "—" : "—";
        var numerica = Enumerable.Range(0, n).Select(i => i > 0 && tab.Linhas.All(l => PareceNumero(Cel(l, i)))).ToArray();
        var largura = Enumerable.Range(0, n).Select(i => numerica[i]
            ? Math.Max(6f, tab.Colunas[i].Length * 0.8f)
            : Math.Clamp(tab.Linhas.Select(l => Cel(l, i).Length).DefaultIfEmpty(0).Max(), Math.Max(6, tab.Colunas[i].Length), motivos && i == 0 ? 60 : 45))
            .ToArray();

        col.Item().PaddingBottom(2).Text(tab.Titulo).FontSize(9).Bold();
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                foreach (var w in largura) d.RelativeColumn(w);
            });
            t.Header(h =>
            {
                for (var i = 0; i < n; i++)
                {
                    var cel = h.Cell().Element(CabCelula);
                    (numerica[i] ? cel.AlignRight() : cel).Text(tab.Colunas[i]);
                }
            });
            foreach (var linha in tab.Linhas)
                for (var i = 0; i < n; i++)
                {
                    var cel = t.Cell().BorderBottom(0.4f).BorderColor(Linha).PaddingVertical(2).PaddingHorizontal(3);
                    (numerica[i] ? cel.AlignRight() : cel).Text(Cel(linha, i)).FontSize(7.8f);
                }
        });
        if (!string.IsNullOrWhiteSpace(tab.Nota))
            col.Item().PaddingTop(2).Text(tab.Nota!).FontSize(7).FontColor(Suave);
    });

    private static TabelaIndicadorDto Motivos(TabelaIndicadorDto t) =>
        t with
        {
            Linhas = [.. t.Linhas.Where(l => !MotivosNoFim.Contains(l.FirstOrDefault())),
                      .. t.Linhas.Where(l => MotivosNoFim.Contains(l.FirstOrDefault()))],
        };

    private static IContainer CabCelula(IContainer c) =>
        c.BorderBottom(0.8f).BorderColor(Suave).PaddingVertical(2.5f).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.FontSize(7.3f).FontColor(Suave).SemiBold());

    private static void Selo(IContainer c, SeloIndicador selo)
    {
        var (texto, fundo, cor) = selo switch
        {
            SeloIndicador.Oficial => ("Oficial", "#dcfce7", "#166534"),
            SeloIndicador.Calculado => ("Calculado", "#dbeafe", "#1e40af"),
            SeloIndicador.Parcial => ("Parcial", "#fef3c7", "#92400e"),
            _ => ("Indisponível", "#f3f4f6", "#4b5563"),
        };
        c.AlignLeft().Background(fundo).PaddingHorizontal(3).PaddingVertical(0.5f).Text(texto).FontSize(6.3f).FontColor(cor).SemiBold();
    }

    // ------------------------------------------------------------------------------------ gráfico
    private sealed record GraficoSvg(string Svg, IReadOnlyList<string> Legenda);

    /// <summary>Barras (lado a lado) ou empilhadas, meses no eixo X com um respiro entre os anos.</summary>
    private GraficoSvg? Grafico(SecaoIndicadorDto s)
    {
        if (s.Grafico is not { } g) return null;
        var meses = dados.Meses;
        var series = s.Series.Where(x => g.Series.Contains(x.Rotulo) && meses.Any(m => x.Valores.GetValueOrDefault(m) is not null)).ToList();
        if (series.Count == 0) return null;
        var empilhado = g.Tipo == "empilhado";
        decimal V(SerieIndicadorDto x, string m) => x.Valores.GetValueOrDefault(m) ?? 0;
        var topo = empilhado ? meses.Max(m => series.Sum(x => V(x, m))) : meses.Max(m => series.Max(x => V(x, m)));
        if (topo <= 0) return null;

        const double W = 1000, H = 190, ML = 56, MR = 10, MT = 12, MB = 40, GapAno = 18;
        double pw = W - ML - MR, ph = H - MT - MB;
        var anos = meses.Select(m => m[..4]).Distinct().ToList();
        var slot = (pw - GapAno * (anos.Count - 1)) / meses.Count;
        var tick = Tick((double)topo);
        var ymax = tick * Math.Max(1, Math.Ceiling((double)topo / tick));
        double Y(double v) => MT + ph - ph * (v / ymax);
        double X0(int i) => ML + i * slot + GapAno * anos.IndexOf(meses[i][..4]);
        string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {W} {H}\" width=\"{W}\" height=\"{H}\" font-family=\"Helvetica, Arial, sans-serif\">");
        for (var k = 0; k * tick <= ymax + 1e-9; k++)
        {
            var yy = Y(k * tick);
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{ML}\" x2=\"{W - MR}\" y1=\"{F(yy)}\" y2=\"{F(yy)}\" stroke=\"{(k == 0 ? "#8c959f" : "#e5e7eb")}\" stroke-width=\"1\"/>");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{ML - 6}\" y=\"{F(yy + 3.5)}\" font-size=\"11\" fill=\"{Suave}\" text-anchor=\"end\">{(k * tick).ToString("N0", PtBr)}</text>");
        }
        for (var i = 0; i < meses.Count; i++)
        {
            var m = meses[i];
            var x0 = X0(i);
            var larg = slot * 0.72;
            var xb = x0 + (slot - larg) / 2;
            if (empilhado)
            {
                double acum = 0;
                for (var j = 0; j < series.Count; j++)
                {
                    var v = (double)V(series[j], m);
                    if (v <= 0) continue;
                    var y1 = Y(acum + v);
                    var alt = Math.Max(0, Y(acum) - y1 - (acum > 0 ? 2 : 0));
                    sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(xb)}\" y=\"{F(y1)}\" width=\"{F(larg)}\" height=\"{F(alt)}\" rx=\"1.5\" fill=\"{Cores[j % Cores.Length]}\"/>");
                    acum += v;
                }
            }
            else
            {
                var bw = (larg - 2 * (series.Count - 1)) / series.Count;
                for (var j = 0; j < series.Count; j++)
                {
                    if (series[j].Valores.GetValueOrDefault(m) is not { } v) continue;
                    var y1 = Y((double)v);
                    sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(xb + j * (bw + 2))}\" y=\"{F(y1)}\" width=\"{F(bw)}\" height=\"{F(Math.Max(0, MT + ph - y1))}\" rx=\"1.5\" fill=\"{Cores[j % Cores.Length]}\"/>");
                }
            }
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(x0 + slot / 2)}\" y=\"{MT + ph + 14}\" font-size=\"11\" fill=\"{Suave}\" text-anchor=\"middle\">{MesesAbr[int.Parse(m[5..], CultureInfo.InvariantCulture) - 1]}</text>");
        }
        foreach (var ano in anos)
        {
            var idx = Enumerable.Range(0, meses.Count).Where(i => meses[i].StartsWith(ano, StringComparison.Ordinal)).ToList();
            var xa = (X0(idx[0]) + X0(idx[^1]) + slot) / 2;
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(xa)}\" y=\"{MT + ph + 32}\" font-size=\"12\" font-weight=\"bold\" fill=\"{Tinta}\" text-anchor=\"middle\">{ano}</text>");
        }
        sb.Append("</svg>");
        return new GraficoSvg(sb.ToString(), series.Select(x => x.Rotulo).ToList());
    }

    private static double Tick(double topo)
    {
        var mag = Math.Pow(10, Math.Floor(Math.Log10(topo)));
        foreach (var p in new[] { 1, 2, 2.5, 5, 10 })
            if (topo <= p * mag * 4) return p * mag;
        return 10 * mag;
    }

    // ------------------------------------------------------------------------------------ metodologia
    private void Metodologia(IContainer c) => c.EnsureSpace(140).Column(col =>
    {
        col.Spacing(3);
        col.Item().Text("Metodologia e limitações").FontSize(12.5f).Bold().FontColor(idv.CorEscura);
        col.Item().LineHorizontal(0.5f).LineColor(Linha);
        foreach (var (selo, texto) in new[]
        {
            (SeloIndicador.Oficial, "Número lido do próprio sistema de origem (ou do espelho fiel dele mantido pela Secretaria)."),
            (SeloIndicador.Calculado, "Derivado a partir de dados oficiais; a regra do cálculo está no texto da seção ou na nota do indicador."),
            (SeloIndicador.Parcial, "Sabidamente incompleto — o valor é um piso; a nota diz o que falta."),
            (SeloIndicador.Indisponivel, "O sistema de origem não fornece esse dado ao município."),
        })
            col.Item().Row(r =>
            {
                r.ConstantItem(62).Element(x => Selo(x, selo));
                r.RelativeItem().Text(texto).FontSize(8);
            });
        col.Item().PaddingTop(4).Text("Coluna \"No período\": soma dos meses para contagens; último mês para estoques (fila); para percentuais e tempos, o valor do ano calculado sobre todos os casos (razão das somas, mediana de todos os casos) — nunca a média dos meses. Datas agrupadas pelo horário de Brasília. Meses em aberto não entram.")
            .FontSize(7.5f).FontColor(Suave);
    });

    // ------------------------------------------------------------------------------------ utilitários
    private IEnumerable<(string Ano, IReadOnlyList<string> Meses)> Anos() =>
        dados.Meses.GroupBy(m => m[..4]).Select(g => (g.Key, (IReadOnlyList<string>)g.ToList()));

    private string Periodo()
    {
        if (dados.Meses.Count == 0) return "—";
        string Longo(string m) => $"{MesesAbr[int.Parse(m[5..], CultureInfo.InvariantCulture) - 1]}/{m[..4]}";
        return $"{Longo(dados.Meses[0])} a {Longo(dados.Meses[^1])}";
    }

    private static string RotuloAno(string ano, IReadOnlyList<string> meses)
    {
        if (meses.Count is 12 or 0) return ano;
        var ini = MesesAbr[int.Parse(meses[0][5..], CultureInfo.InvariantCulture) - 1];
        var fim = MesesAbr[int.Parse(meses[^1][5..], CultureInfo.InvariantCulture) - 1];
        return ini == fim ? $"{ano} ({ini})" : $"{ano} ({ini}–{fim})";
    }

    /// <summary>Fechamento do bloco do ano — mesma regra da tela e do relatório.</summary>
    private static (decimal? Valor, string Tipo) Agregado(SerieIndicadorDto s, string ano, IReadOnlyList<string> meses)
    {
        if (s.Anual.GetValueOrDefault(ano) is { } anual) return (anual, "no ano");
        var vals = meses.Select(m => s.Valores.GetValueOrDefault(m)).Where(v => v is not null).Select(v => v!.Value).ToList();
        if (vals.Count == 0) return (null, "");
        return s.Agregacao switch
        {
            AgregacaoIndicador.UltimoMes => (vals[^1], "último"),
            AgregacaoIndicador.Media => (Math.Round(vals.Average(), 1), "média"),
            _ => (vals.Sum(), "total"),
        };
    }

    private static string Valor(decimal? v, FormatoIndicador f) => v switch
    {
        null => "—",
        { } x when f == FormatoIndicador.Percentual => x.ToString("0.0", PtBr) + "%",
        { } x when x != Math.Truncate(x) => x.ToString("N1", PtBr),
        { } x => x.ToString("N0", PtBr),
    };

    private static string Dias(int? d) => d?.ToString(PtBr) ?? "—";

    private static bool PareceNumero(string v) =>
        v.Length > 0 && v.All(ch => char.IsDigit(ch) || ch is '.' or ',' or '%' or '-' or '—' or ' ');

    private static DateTime FusoHorario(DateTime utc) => Common.Tempo.FusoBrasilia.ParaExibicao(utc);
}
