using Microsoft.Extensions.Logging;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;

/// <summary>
/// Aplica um lote já lido e informa até onde a varredura chegou.
///
/// <para><paramref name="cursorConcluido"/> é a <b>primeira data ainda NÃO varrida</b>: tudo antes
/// dela, nesta situação, já está no banco. Gravar o lote e o cursor na mesma transação é o que
/// torna a rodada retomável de verdade — cursor à frente do que foi gravado viraria buraco
/// silencioso na base.</para>
/// </summary>
public delegate Task AplicarLoteSer(
    IReadOnlyList<SerLinhaGrade> linhas, DateOnly cursorConcluido, CancellationToken cancellationToken);

/// <summary>
/// Varredura da grade pelo <b>export de 500</b> da tela de Histórico — substitui a paginação de 20
/// em 20 da tela de Solicitação (ADR-0042 §5, revisto em 06/08/2026).
///
/// <para><b>Janela adaptativa sobre <c>Data da Solicitação</c>, varrendo da esquerda para a
/// direita.</b> Tenta a maior janela possível; quando o SER avisa que cortou em 500, corta a janela
/// ao meio e refaz <i>sem avançar</i>; quando cabe, aplica o lote, avança o cursor e vai
/// aumentando a janela de novo. A data da solicitação é imutável, então as mesmas fatias saem
/// iguais entre execuções.</para>
///
/// <para><b>Por que não o cursor por <c>max(data)</c> do lote</b> (a ideia original do handoff):
/// ele só é correto se o corte de 500 for feito <i>depois</i> de ordenar por data da solicitação —
/// e isso nunca foi provado. Se o SER cortar em qualquer outra ordem, pular para a maior data lida
/// <b>saltaria por cima</b> das solicitações mais antigas que ficaram de fora do lote, em silêncio.
/// É exatamente a classe de perda invisível que já custou ~35% da base. A janela adaptativa custa
/// algumas requisições a mais e é correta sob qualquer ordenação; se um dia a ordenação for
/// confirmada, dá para trocar só este laço.</para>
/// </summary>
public sealed class VarredorSerPorExport(
    ISerExportLeitor leitor,
    ILogger<VarredorSerPorExport> logger)
{
    /// <summary>Teto declarado pela tela de Histórico ("retorno limitado em 500 resultados").</summary>
    private const int TetoExport = 500;

    /// <summary>Abaixo disso o lote está folgado e a janela pode dobrar.</summary>
    private const int FolgaParaCrescer = TetoExport / 2;

    public async Task<ResultadoVarreduraSer> VarrerAsync(
        SituacaoSer situacao,
        DateOnly inicio,
        DateOnly fim,
        AplicarLoteSer aplicar,
        CancellationToken cancellationToken,
        ResultadoVarreduraSer? acumulado = null)
    {
        var resultado = acumulado ?? new ResultadoVarreduraSer();

        var cursor = inicio;
        var passo = DiasEntre(inicio, fim);
        var passoMaximo = passo;

        while (cursor <= fim)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var janelaFim = Menor(fim, cursor.AddDays(passo - 1));
            var lote = await ExportarAsync(situacao, cursor, janelaFim, null, resultado, cancellationToken);

            if (lote.Truncado)
            {
                if (janelaFim > cursor)
                {
                    // Não avança: a janela inteira é refeita menor, senão o pedaço que não coube
                    // nos 500 ficaria para trás sem ninguém notar.
                    passo = Math.Max(1, passo / 2);
                    logger.LogInformation(
                        "SER/export: corte em {Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} — "
                        + "encolhendo a janela para {Passo} dia(s).",
                        situacao, cursor, janelaFim, passo);
                    continue;
                }

                // Um único dia estoura 500: só resta fatiar por Tipo.
                await VarrerDiaPorTipoAsync(situacao, cursor, aplicar, resultado, cancellationToken);
                cursor = cursor.AddDays(1);
                passo = 1;
                await aplicar([], cursor, cancellationToken);
                continue;
            }

            await aplicar(lote.Linhas, janelaFim.AddDays(1), cancellationToken);
            cursor = janelaFim.AddDays(1);

            // Trecho folgado (ou vazio) da linha do tempo: dobra a janela para não gastar uma
            // requisição por semana em 2022, quando o volume era baixo.
            if (lote.Linhas.Count <= FolgaParaCrescer)
            {
                passo = Math.Min(Math.Max(passo * 2, 1), passoMaximo);
            }
        }

        return resultado;
    }

    /// <summary>
    /// Último recurso: o dia inteiro não coube em 500, então lê CONSULTA e EXAME separados. O que
    /// ainda assim não couber vira <see cref="FatiaTruncada"/> — registros <b>não lidos</b>,
    /// declarados, que impedem a rodada de ser marcada como Concluída.
    /// </summary>
    private async Task VarrerDiaPorTipoAsync(
        SituacaoSer situacao,
        DateOnly dia,
        AplicarLoteSer aplicar,
        ResultadoVarreduraSer resultado,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SER/export: {Dia:dd/MM/yyyy} ({Situacao}) passa de 500 num dia só — fatiando por Tipo.",
            dia, situacao);

        foreach (var tipo in new[] { TipoRecursoSer.Consulta, TipoRecursoSer.Exame })
        {
            var lote = await ExportarAsync(situacao, dia, dia, tipo, resultado, cancellationToken);

            // O lote é aplicado mesmo truncado: o que foi lido é real e vale espelhar. O que se
            // perde fica declarado na fatia truncada abaixo.
            await aplicar(lote.Linhas, dia, cancellationToken);

            if (!lote.Truncado) continue;

            resultado.Truncadas.Add(new FatiaTruncada(situacao, dia, tipo));
            logger.LogWarning(
                "SER/export: FATIA TRUNCADA — {Situacao} {Dia:dd/MM/yyyy} tipo={Tipo} passa de 500 "
                + "registros. Há solicitações NÃO lidas nesse recorte.",
                situacao, dia, tipo);
        }
    }

    private async Task<LoteExportSer> ExportarAsync(
        SituacaoSer situacao,
        DateOnly inicio,
        DateOnly fim,
        TipoRecursoSer? tipo,
        ResultadoVarreduraSer resultado,
        CancellationToken cancellationToken)
    {
        var lote = await leitor.ExportarAsync(
            new SerFiltroExport
            {
                Situacao = situacao,
                Tipo = tipo,
                DataSolicitacaoInicio = inicio,
                DataSolicitacaoFim = fim,
            },
            cancellationToken);

        resultado.Buscas++;
        return lote;
    }

    private static int DiasEntre(DateOnly inicio, DateOnly fim) =>
        Math.Max(1, fim.DayNumber - inicio.DayNumber + 1);

    private static DateOnly Menor(DateOnly a, DateOnly b) => a < b ? a : b;
}
