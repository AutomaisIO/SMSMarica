using SMSMais.Core.Integracoes.SerWeb.Varredura.Export;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Integracoes.SerWeb.Varredura;

/// <summary>O que a fase Judicial leu: os IDs que o SER devolve com o filtro "Somente com mandado
/// judicial" e se a leitura cobriu tudo.</summary>
public sealed record LeituraMandadoJudicialSer(IReadOnlySet<string> Ids, ResultadoVarreduraSer Resultado)
{
    /// <summary>Nenhuma fatia estourou o teto. Só leitura completa autoriza gravar "verificado em" na
    /// base inteira — com fatia truncada, quem ficou de fora pareceria conferido e sem mandado.</summary>
    public bool Completa => Resultado.Truncadas.Count == 0;
}

/// <summary>
/// Fase <b>Judicial</b> da varredura do SER: a grade não mostra o martelo, então a única forma de saber
/// quem tem mandado é pesquisar de novo com o checkbox "Somente com mandado judicial" ligado (medido em
/// 30/09/2026: 169 solicitações de Maricá, todas já no espelho — docs/ser.md §12).
///
/// <para>Só a tela de Solicitação tem o filtro, e ela corta em 100 sem avisar: por isso passa pela
/// mesma janela adaptativa da grade (<see cref="VarredorSerPorExport"/>), com o filtro ligado. Custo
/// medido: poucas requisições por situação — há poucos judiciais.</para>
/// </summary>
public static class MandadoJudicialSer
{
    /// <summary>Janela ampla de Data da Solicitação — mandado vale para pedido antigo também.</summary>
    public static readonly DateOnly InicioJanela = new(2010, 1, 1);

    public static async Task<LeituraMandadoJudicialSer> LerAsync(
        ISerExportLeitor leitorSolicitacao,
        VarredorSerPorExport varredor,
        IReadOnlyList<SituacaoSer> situacoes,
        DateOnly fim,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new ResultadoVarreduraSer();

        Task Juntar(IReadOnlyList<SerLinhaGrade> linhas, DateOnly _, CancellationToken __)
        {
            foreach (var l in linhas)
            {
                if (!string.IsNullOrWhiteSpace(l.IdSer)) ids.Add(l.IdSer.Trim());
            }
            return Task.CompletedTask;
        }

        await leitorSolicitacao.PrepararAsync(cancellationToken);
        foreach (var situacao in situacoes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await varredor.VarrerAsync(
                leitorSolicitacao, situacao, InicioJanela, fim, Juntar, cancellationToken, resultado,
                mandadoJudicial: true);
        }

        return new LeituraMandadoJudicialSer(ids, resultado);
    }
}
