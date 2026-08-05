using Microsoft.Extensions.Logging;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura;

/// <summary>Uma fatia que não coube no teto de 100 da tela do SER.</summary>
public sealed record FatiaTruncada(SituacaoSer Situacao, DateOnly Dia, TipoRecursoSer? Tipo);

/// <summary>Resultado de uma varredura por situação.</summary>
public sealed class ResultadoVarreduraSer
{
    /// <summary>Solicitações únicas, deduplicadas por <c>IdSer</c>.</summary>
    public Dictionary<string, SerLinhaGrade> Solicitacoes { get; } = new(StringComparer.Ordinal);

    public int Buscas { get; set; }
    public int Paginas { get; set; }

    /// <summary>Fatias que estouraram o teto mesmo em 1 dia + 1 tipo. Cada uma significa
    /// <b>registros não lidos</b> — nunca truncamos em silêncio.</summary>
    public List<FatiaTruncada> Truncadas { get; } = [];

    public int Total => Solicitacoes.Count;
}

/// <summary>
/// Varredura completa driblando o teto de 100 da tela do SER — ADR-0042 §5.
///
/// <para><b>Bisecção adaptativa por Data da Solicitação.</b> Essa data é imutável (é a data de
/// criação do pedido), então serve de eixo estável de fatiamento: as mesmas fatias saem iguais
/// entre execuções. Pesquisa uma faixa; se o datascroller mostrar 5 páginas (o teto), parte a
/// faixa ao meio e refaz cada metade.</para>
///
/// <para>Faixa de 1 dia que ainda estoure cai no fallback de fatiar por <c>Tipo</c>
/// (CONSULTA/EXAME). O que nem assim couber vai para <see cref="ResultadoVarreduraSer.Truncadas"/>.</para>
/// </summary>
public sealed class VarredorSer(ISerLeitorService leitor, ILogger<VarredorSer> logger)
{
    /// <summary>5 páginas × 20 linhas = 100. É limite da TELA, não do scraper.</summary>
    private const int TetoPaginas = 5;

    public async Task<ResultadoVarreduraSer> VarrerAsync(
        SituacaoSer situacao,
        DateOnly inicio,
        DateOnly fim,
        CancellationToken cancellationToken,
        ResultadoVarreduraSer? acumulado = null,
        TipoRecursoSer? tipo = null)
    {
        var resultado = acumulado ?? new ResultadoVarreduraSer();

        var filtro = new SerFiltroPesquisa
        {
            Situacao = situacao,
            Tipo = tipo,
            DataSolicitacaoInicio = inicio,
            DataSolicitacaoFim = fim,
        };

        var pagina = await leitor.PesquisarAsync(filtro, cancellationToken);
        resultado.Buscas++;

        if (pagina.Paginas >= TetoPaginas)
        {
            if (inicio < fim)
            {
                var meio = inicio.AddDays((fim.DayNumber - inicio.DayNumber) / 2);
                logger.LogInformation(
                    "SER: teto em {Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} — partindo em {Meio:dd/MM/yyyy}.",
                    situacao, inicio, fim, meio);

                await VarrerAsync(situacao, inicio, meio, cancellationToken, resultado, tipo);
                await VarrerAsync(situacao, meio.AddDays(1), fim, cancellationToken, resultado, tipo);
                return resultado;
            }

            if (tipo is null)
            {
                logger.LogInformation(
                    "SER: teto num dia só ({Dia:dd/MM/yyyy}, {Situacao}) — fatiando por Tipo.", inicio, situacao);

                foreach (var t in new[] { TipoRecursoSer.Consulta, TipoRecursoSer.Exame })
                {
                    await VarrerAsync(situacao, inicio, fim, cancellationToken, resultado, t);
                }
                return resultado;
            }

            // Dia único + tipo único e ainda estoura: existem registros que NÃO serão lidos.
            // Registramos e seguimos — a rodada será marcada como Parcial, nunca Concluída.
            resultado.Truncadas.Add(new FatiaTruncada(situacao, inicio, tipo));
            logger.LogWarning(
                "SER: FATIA TRUNCADA — {Situacao} {Dia:dd/MM/yyyy} tipo={Tipo} tem mais de 100 registros.",
                situacao, inicio, tipo);
        }

        Acumular(resultado, pagina.Linhas);

        for (var p = 2; p <= pagina.Paginas; p++)
        {
            var linhas = await leitor.IrParaPaginaAsync(p, cancellationToken);
            if (linhas.Count == 0) break;
            Acumular(resultado, linhas);
        }

        resultado.Paginas += Math.Max(pagina.Paginas, 1);
        return resultado;
    }

    private static void Acumular(ResultadoVarreduraSer resultado, IReadOnlyList<SerLinhaGrade> linhas)
    {
        foreach (var linha in linhas)
        {
            if (!string.IsNullOrWhiteSpace(linha.IdSer)) resultado.Solicitacoes[linha.IdSer] = linha;
        }
    }
}
