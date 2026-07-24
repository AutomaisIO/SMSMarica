using System.Text;
using SMSMarica.Core.Inteligencia.Fontes;

namespace SMSMarica.Core.Inteligencia.Conhecimento.Gestao;

/// <summary>
/// Monta os documentos markdown do modelo da base a partir dos resultados de introspecção:
/// um doc por tabela (colunas + relacionamentos) e um doc-catálogo compacto com todas as tabelas.
/// Ver ADR-0023.
/// </summary>
public static class ModeloMarkdown
{
    public sealed record TabelaDoc(string Caminho, string Titulo, string Conteudo);

    public sealed record Resultado(
        IReadOnlyList<TabelaDoc> Documentos, int TotalTabelas, int Documentadas, int TotalFks);

    public static Resultado Montar(
        ResultadoConsulta colunas, ResultadoConsulta fks, int maxTabelas)
    {
        // ---- índices de coluna por nome (a fonte devolve os aliases fixos da introspecção) ----
        var iEsq = Idx(colunas, "esquema");
        var iTab = Idx(colunas, "tabela");
        var iCol = Idx(colunas, "coluna");
        var iTipo = Idx(colunas, "tipo");
        var iTam = Idx(colunas, "tamanho");
        var iNul = Idx(colunas, "anulavel");

        // agrupa colunas por "esquema.tabela" preservando a ordem de chegada (já vem ORDER BY)
        var tabelas = new List<string>();
        var colsPorTabela = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (var linha in colunas.Linhas)
        {
            var esq = Txt(linha, iEsq);
            var tab = Txt(linha, iTab);
            var chave = string.IsNullOrEmpty(esq) ? tab : $"{esq}.{tab}";
            if (!colsPorTabela.TryGetValue(chave, out var lista))
            {
                lista = [];
                colsPorTabela[chave] = lista;
                tabelas.Add(chave);
            }
            lista.Add([Txt(linha, iCol), Txt(linha, iTipo), Txt(linha, iTam), Txt(linha, iNul)]);
        }

        // ---- FKs agrupadas por tabela de origem ----
        var fkOrig = Idx(fks, "tabela_origem");
        var fkColO = Idx(fks, "coluna_origem");
        var fkDest = Idx(fks, "tabela_destino");
        var fkColD = Idx(fks, "coluna_destino");
        var fkPorTabela = new Dictionary<string, List<(string, string, string)>>(StringComparer.OrdinalIgnoreCase);
        var totalFks = 0;
        if (fks.Sucesso)
        {
            foreach (var linha in fks.Linhas)
            {
                var origem = Txt(linha, fkOrig);
                if (!fkPorTabela.TryGetValue(origem, out var lista))
                {
                    lista = [];
                    fkPorTabela[origem] = lista;
                }
                lista.Add((Txt(linha, fkColO), Txt(linha, fkDest), Txt(linha, fkColD)));
                totalFks++;
            }
        }

        // ---- catálogo (todas as tabelas, compacto) ----
        var catalogo = new StringBuilder();
        catalogo.AppendLine("# Catálogo de tabelas");
        catalogo.AppendLine();
        catalogo.AppendLine($"A base tem **{tabelas.Count} tabelas**. Cada uma tem um documento próprio ")
            .AppendLine("com colunas e relacionamentos. Este catálogo é o mapa geral.");
        catalogo.AppendLine();
        catalogo.AppendLine("| Tabela | Colunas | FKs saindo |");
        catalogo.AppendLine("|---|---|---|");
        foreach (var t in tabelas)
        {
            var nCols = colsPorTabela[t].Count;
            var nFks = fkPorTabela.TryGetValue(t, out var l) ? l.Count : 0;
            catalogo.AppendLine($"| {t} | {nCols} | {nFks} |");
        }

        var docs = new List<TabelaDoc>
        {
            new("modelo/00-catalogo.md", "Catálogo de tabelas", catalogo.ToString().TrimEnd()),
        };

        // ---- um doc por tabela, até o teto (sem truncar em silêncio) ----
        var documentar = tabelas.Take(maxTabelas).ToList();
        foreach (var t in documentar)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Tabela {t}");
            sb.AppendLine();
            sb.AppendLine("## Colunas");
            sb.AppendLine();
            sb.AppendLine("| Coluna | Tipo | Nulo? |");
            sb.AppendLine("|---|---|---|");
            foreach (var c in colsPorTabela[t])
            {
                var tipo = string.IsNullOrEmpty(c[2]) ? c[1] : $"{c[1]}({c[2]})";
                var nulo = c[3].StartsWith("N", StringComparison.OrdinalIgnoreCase) ? "não" : "sim";
                sb.AppendLine($"| {c[0]} | {tipo} | {nulo} |");
            }

            if (fkPorTabela.TryGetValue(t, out var relacoes) && relacoes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Relacionamentos (chaves estrangeiras)");
                sb.AppendLine();
                foreach (var (colO, dest, colD) in relacoes)
                {
                    sb.AppendLine($"- `{colO}` → **{dest}**.`{colD}`");
                }
            }

            var caminho = "modelo/" + t.Replace('.', '-').ToLowerInvariant() + ".md";
            docs.Add(new TabelaDoc(caminho, $"Tabela {t}", sb.ToString().TrimEnd()));
        }

        return new Resultado(docs, tabelas.Count, documentar.Count, totalFks);
    }

    private static int Idx(ResultadoConsulta r, string coluna)
    {
        for (var i = 0; i < r.Colunas.Count; i++)
        {
            if (string.Equals(r.Colunas[i], coluna, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static string Txt(IReadOnlyList<object?> linha, int idx) =>
        idx < 0 || idx >= linha.Count ? "" : (linha[idx]?.ToString() ?? "").Trim();
}
