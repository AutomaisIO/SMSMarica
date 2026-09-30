using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura;

/// <summary>O que a fase Judicial leu: os IDs que o SERNIT devolve com o filtro "Somente com mandado
/// judicial" e se a leitura cobriu tudo.</summary>
public sealed record LeituraMandadoJudicialSernit(IReadOnlySet<string> Ids, ResultadoVarreduraSernit Resultado)
{
    /// <summary>Nenhuma fatia estourou o teto. Só leitura completa autoriza gravar "verificado em" na
    /// base inteira.</summary>
    public bool Completa => Resultado.Truncadas.Count == 0;
}

/// <summary>
/// Fase <b>Judicial</b> da varredura do SERNIT: pesquisa de novo com o checkbox "Somente com mandado
/// judicial" ligado — a grade não mostra o martelo. Medido em 30/09/2026: zero para Maricá em todas as
/// situações, com controle (docs/APRENDIZADOS.md §8 do laboratório). Mesma janela adaptativa da grade
/// (teto de 100 com total real no aviso).
/// </summary>
public static class MandadoJudicialSernit
{
    /// <summary>Janela ampla de Data da Solicitação — mandado vale para pedido antigo também.</summary>
    public static readonly DateOnly InicioJanela = new(2010, 1, 1);

    public static async Task<LeituraMandadoJudicialSernit> LerAsync(
        VarredorSernitPorPaginacao varredor,
        IReadOnlyList<SituacaoSernit> situacoes,
        DateOnly fim,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new ResultadoVarreduraSernit();

        Task Juntar(IReadOnlyList<SernitLinhaGrade> linhas, DateOnly _, CancellationToken __)
        {
            foreach (var l in linhas)
            {
                if (!string.IsNullOrWhiteSpace(l.IdSernit)) ids.Add(l.IdSernit.Trim());
            }
            return Task.CompletedTask;
        }

        foreach (var situacao in situacoes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await varredor.VarrerAsync(situacao, InicioJanela, fim, Juntar, cancellationToken, resultado,
                mandadoJudicial: true);
        }

        return new LeituraMandadoJudicialSernit(ids, resultado);
    }
}
