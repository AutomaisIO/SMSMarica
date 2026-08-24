using Microsoft.Extensions.Logging;
using SMSMais.Data.Entities.Ser;

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
/// Varredura da grade <b>por arquivo exportado</b> — nunca por paginação (ADR-0042 §5, revisto em
/// 06 e 08/08/2026).
///
/// <para><b>Serve as duas telas do SER.</b> A de Histórico devolve 500 por lote e avisa por escrito
/// quando corta: é o caminho das seis situações que ela oferece. A de Solicitação devolve 100 e não
/// avisa nada, mas é a <b>única com ALTA</b>. Em ambas o motor baixa o arquivo em vez de paginar —
/// a leitura de 20 em 20 tinha caminhos de perda silenciosa e foi por eles que a varredura de
/// 07/08/2026 perdeu 853 registros de ALTA <i>declarando cobertura completa</i>.</para>
///
/// <para><b>Janela adaptativa sobre <c>Data da Solicitação</c>, varrendo da esquerda para a
/// direita.</b> Tenta a maior janela possível; quando o lote vem cortado, parte a janela ao meio e
/// refaz <i>sem avançar</i>; quando cabe, aplica o lote, avança o cursor e vai aumentando a janela
/// de novo. A data da solicitação é imutável, então as mesmas fatias saem iguais entre execuções.</para>
///
/// <para><b>Por que não o cursor por <c>max(data)</c> do lote</b> (a ideia original do handoff):
/// ele só é correto se o corte de 500 for feito <i>depois</i> de ordenar por data da solicitação —
/// e isso nunca foi provado. Se o SER cortar em qualquer outra ordem, pular para a maior data lida
/// <b>saltaria por cima</b> das solicitações mais antigas que ficaram de fora do lote, em silêncio.
/// É exatamente a classe de perda invisível que já custou ~35% da base. A janela adaptativa custa
/// algumas requisições a mais e é correta sob qualquer ordenação; se um dia a ordenação for
/// confirmada, dá para trocar só este laço.</para>
/// </summary>
public sealed class VarredorSerPorExport(ILogger<VarredorSerPorExport> logger)
{
    /// <summary>
    /// Varre uma situação inteira pelo export da tela que <paramref name="leitor"/> representa.
    ///
    /// <para>O leitor entra por parâmetro (e não pelo construtor) porque a varredura usa <b>duas</b>
    /// telas: a de Histórico para as seis situações que ela oferece, e a de Solicitação para ALTA,
    /// que só existe lá. A lógica de janela é a mesma; o que muda é o teto e como cada tela avisa
    /// que cortou — uma por escrito, a outra só devolvendo o lote cheio.</para>
    /// </summary>
    public async Task<ResultadoVarreduraSer> VarrerAsync(
        ISerExportLeitor leitor,
        SituacaoSer situacao,
        DateOnly inicio,
        DateOnly fim,
        AplicarLoteSer aplicar,
        CancellationToken cancellationToken,
        ResultadoVarreduraSer? acumulado = null)
    {
        var resultado = acumulado ?? new ResultadoVarreduraSer();

        // Abaixo da metade do teto o lote está folgado e a janela pode dobrar.
        var folgaParaCrescer = leitor.TetoPorLote / 2;

        var cursor = inicio;
        var passo = DiasEntre(inicio, fim);
        var passoMaximo = passo;

        while (cursor <= fim)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var janelaFim = Menor(fim, cursor.AddDays(passo - 1));
            var lote = await ExportarAsync(leitor, situacao, cursor, janelaFim, null, resultado, cancellationToken);

            if (lote.Truncado)
            {
                if (janelaFim > cursor)
                {
                    // Não avança: a janela inteira é refeita menor, senão o pedaço que não coube
                    // no teto ficaria para trás sem ninguém notar.
                    //
                    // O passo é primeiro CLAMPADO na janela real antes de ser partido ao meio:
                    // perto do fim do intervalo `janelaFim` já vem grudado em `fim`, e partir um
                    // passo grande que não encosta na janela repetia o MESMO export truncado
                    // várias vezes contra a produção do Estado sem trazer nada novo.
                    passo = Math.Max(1, DiasEntre(cursor, janelaFim) / 2);
                    logger.LogInformation(
                        "SER/export: corte em {Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} — "
                        + "encolhendo a janela para {Passo} dia(s).",
                        situacao, cursor, janelaFim, passo);
                    continue;
                }

                // Um único dia estoura o teto: só resta fatiar por Tipo.
                await VarrerDiaPorTipoAsync(leitor, situacao, cursor, aplicar, resultado, cancellationToken);
                cursor = cursor.AddDays(1);
                passo = 1;
                await aplicar([], cursor, cancellationToken);
                continue;
            }

            await aplicar(lote.Linhas, janelaFim.AddDays(1), cancellationToken);
            cursor = janelaFim.AddDays(1);

            // Trecho folgado (ou vazio) da linha do tempo: dobra a janela para não gastar uma
            // requisição por semana em 2022, quando o volume era baixo.
            if (lote.Linhas.Count <= folgaParaCrescer)
            {
                passo = Math.Min(Math.Max(passo * 2, 1), passoMaximo);
            }
        }

        return resultado;
    }

    /// <summary>
    /// Último recurso: o dia inteiro não coube no teto, então lê CONSULTA e EXAME separados. O que
    /// ainda assim não couber vira <see cref="FatiaTruncada"/> — registros <b>não lidos</b>,
    /// declarados, que impedem a rodada de ser marcada como Concluída.
    ///
    /// <para><b>Só é confiável na tela de Solicitação.</b> Na de Histórico o combo de Tipo é
    /// decorativo (medido: com CONSULTA ela devolve linhas de EXAME), então lá esta última fatia
    /// tende a não separar nada e o recorte acaba declarado truncado — que é o comportamento
    /// correto: declarar perda em vez de fingir cobertura.</para>
    /// </summary>
    private async Task VarrerDiaPorTipoAsync(
        ISerExportLeitor leitor,
        SituacaoSer situacao,
        DateOnly dia,
        AplicarLoteSer aplicar,
        ResultadoVarreduraSer resultado,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SER/export: {Dia:dd/MM/yyyy} ({Situacao}) passa de {Teto} num dia só — fatiando por Tipo.",
            dia, situacao, leitor.TetoPorLote);

        foreach (var tipo in new[] { TipoRecursoSer.Consulta, TipoRecursoSer.Exame })
        {
            var lote = await ExportarAsync(leitor, situacao, dia, dia, tipo, resultado, cancellationToken);

            // O lote é aplicado mesmo truncado: o que foi lido é real e vale espelhar. O que se
            // perde fica declarado na fatia truncada abaixo.
            await aplicar(lote.Linhas, dia, cancellationToken);

            if (!lote.Truncado) continue;

            resultado.Truncadas.Add(new FatiaTruncada(situacao, dia, tipo));
            logger.LogWarning(
                "SER/export: FATIA TRUNCADA — {Situacao} {Dia:dd/MM/yyyy} tipo={Tipo} passa de "
                + "{Teto} registros na tela {Tela}. Há solicitações NÃO lidas nesse recorte.",
                situacao, dia, tipo, leitor.TetoPorLote, leitor.Tela);
        }
    }

    private async Task<LoteExportSer> ExportarAsync(
        ISerExportLeitor leitor,
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
