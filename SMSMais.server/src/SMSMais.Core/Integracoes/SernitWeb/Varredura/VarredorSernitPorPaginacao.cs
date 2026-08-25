using Microsoft.Extensions.Logging;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura;

/// <summary>
/// Aplica um lote já lido e informa até onde a varredura chegou. <paramref name="cursorConcluido"/>
/// é a <b>primeira data ainda NÃO varrida</b> — gravar lote + cursor na mesma transação é o que
/// torna a rodada retomável (cursor à frente do gravado viraria buraco silencioso).
/// </summary>
public delegate Task AplicarLoteSernit(
    IReadOnlyList<SernitLinhaGrade> linhas, DateOnly cursorConcluido, CancellationToken cancellationToken);

/// <summary>
/// Varredura da grade do SERNIT <b>por PAGINAÇÃO</b> — o SERNIT não tem Exportar (difere do SER-RJ,
/// que baixa .xls). A tela lê no máximo 100 (5 páginas × 20) mas informa o total real em
/// <c>form0:msgErro</c> (<c>SernitPaginaGrade.TotalReal</c>/<c>Capada</c>).
///
/// <para><b>Janela adaptativa sobre <c>Data da Solicitação</c></b> (imutável), da esquerda para a
/// direita — a mesma lógica do <c>VarredorSerPorExport</c>: tenta a maior janela; se a grade veio
/// capada, parte a janela ao meio e refaz SEM avançar; quando cabe, lê todas as páginas, aplica o
/// lote e avança. Um único dia que ainda estoure é fatiado por Tipo; o que nem assim couber vira
/// <see cref="FatiaTruncadaSernit"/> — registros declarados como NÃO lidos.</para>
/// </summary>
public sealed class VarredorSernitPorPaginacao(
    ISernitLeitorService leitor,
    ILogger<VarredorSernitPorPaginacao> logger)
{
    private const int Teto = 100;
    private const int PorPagina = 20;

    public async Task<ResultadoVarreduraSernit> VarrerAsync(
        SituacaoSernit situacao,
        DateOnly inicio,
        DateOnly fim,
        AplicarLoteSernit aplicar,
        CancellationToken cancellationToken,
        ResultadoVarreduraSernit? acumulado = null)
    {
        var resultado = acumulado ?? new ResultadoVarreduraSernit();
        var folgaParaCrescer = Teto / 2;

        var cursor = inicio;
        var passo = DiasEntre(inicio, fim);
        var passoMaximo = passo;

        while (cursor <= fim)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var janelaFim = Menor(fim, cursor.AddDays(passo - 1));
            var pg = await leitor.PesquisarAsync(
                Filtro(situacao, null, cursor, janelaFim), cancellationToken);
            resultado.Buscas++;

            if (pg.Capada)
            {
                if (janelaFim > cursor)
                {
                    // Não avança: a janela inteira é refeita menor (o pedaço que não coube ficaria
                    // para trás sem ninguém notar). Passo clampado na janela real antes de partir.
                    passo = Math.Max(1, DiasEntre(cursor, janelaFim) / 2);
                    logger.LogInformation(
                        "SERNIT: corte (>{Teto}) em {Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy}"
                        + "{Total} — encolhendo a janela para {Passo} dia(s).",
                        Teto, situacao, cursor, janelaFim,
                        pg.TotalReal is { } t ? $" (total real {t})" : string.Empty, passo);
                    continue;
                }

                // Um único dia estoura o teto: só resta fatiar por Tipo.
                await VarrerDiaPorTipoAsync(situacao, cursor, aplicar, resultado, cancellationToken);
                cursor = cursor.AddDays(1);
                passo = 1;
                await aplicar([], cursor, cancellationToken);
                continue;
            }

            // Cabe: lê todas as páginas (2..N) e aplica o lote completo.
            var linhas = await LerTodasPaginasAsync(pg, resultado, cancellationToken);
            await aplicar(linhas, janelaFim.AddDays(1), cancellationToken);
            cursor = janelaFim.AddDays(1);

            if (linhas.Count <= folgaParaCrescer)
            {
                passo = Math.Min(Math.Max(passo * 2, 1), passoMaximo);
            }
        }

        return resultado;
    }

    /// <summary>Último recurso: o dia inteiro não coube, então lê CONSULTA e EXAME separados. O que
    /// ainda estoura vira fatia truncada, mas as 100 legíveis são aplicadas (dado lido é dado real).</summary>
    private async Task VarrerDiaPorTipoAsync(
        SituacaoSernit situacao,
        DateOnly dia,
        AplicarLoteSernit aplicar,
        ResultadoVarreduraSernit resultado,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SERNIT: {Dia:dd/MM/yyyy} ({Situacao}) passa de {Teto} num dia só — fatiando por Tipo.",
            dia, situacao, Teto);

        foreach (var tipo in new[] { TipoRecursoSernit.Consulta, TipoRecursoSernit.Exame })
        {
            var pg = await leitor.PesquisarAsync(Filtro(situacao, tipo, dia, dia), cancellationToken);
            resultado.Buscas++;

            var linhas = await LerTodasPaginasAsync(pg, resultado, cancellationToken);
            await aplicar(linhas, dia, cancellationToken);

            if (!pg.Capada) continue;

            resultado.Truncadas.Add(new FatiaTruncadaSernit(situacao, dia, tipo));
            logger.LogWarning(
                "SERNIT: FATIA TRUNCADA — {Situacao} {Dia:dd/MM/yyyy} tipo={Tipo} passa de {Teto} "
                + "registros (total real {Total}). Há solicitações NÃO lidas nesse recorte.",
                situacao, dia, tipo, Teto, pg.TotalReal?.ToString() ?? "?");
        }
    }

    /// <summary>Lê da página 1 (já pesquisada) até a última que o datascroller expõe, deduplicando
    /// por <c>IdSernit</c>.</summary>
    private async Task<IReadOnlyList<SernitLinhaGrade>> LerTodasPaginasAsync(
        SernitPaginaGrade pg, ResultadoVarreduraSernit resultado, CancellationToken cancellationToken)
    {
        var todas = new Dictionary<string, SernitLinhaGrade>(StringComparer.Ordinal);
        foreach (var l in pg.Linhas)
        {
            if (!string.IsNullOrWhiteSpace(l.IdSernit)) todas[l.IdSernit] = l;
        }
        resultado.Paginas++;

        for (var pagina = 2; pagina <= pg.Paginas; pagina++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linhas = await leitor.IrParaPaginaAsync(pagina, cancellationToken);
            resultado.Paginas++;
            foreach (var l in linhas)
            {
                if (!string.IsNullOrWhiteSpace(l.IdSernit)) todas[l.IdSernit] = l;
            }
        }

        return todas.Values.ToList();
    }

    private static SernitFiltroPesquisa Filtro(
        SituacaoSernit situacao, TipoRecursoSernit? tipo, DateOnly inicio, DateOnly fim) => new()
    {
        Situacao = situacao,
        Tipo = tipo,
        DataSolicitacaoInicio = inicio,
        DataSolicitacaoFim = fim,
    };

    private static int DiasEntre(DateOnly inicio, DateOnly fim) =>
        Math.Max(1, fim.DayNumber - inicio.DayNumber + 1);

    private static DateOnly Menor(DateOnly a, DateOnly b) => a < b ? a : b;
}
