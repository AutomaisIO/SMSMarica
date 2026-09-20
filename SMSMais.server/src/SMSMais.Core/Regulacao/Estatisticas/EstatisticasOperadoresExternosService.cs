using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.SisregWeb.Estatisticas;
using SMSMais.Core.Regulacao.Estatisticas.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Estatisticas;

public interface IEstatisticasOperadoresExternosService
{
    /// <summary>Todos os nomes que já assinaram um evento, com a marca de quem entra nas estatísticas.</summary>
    Task<OperadoresExternosConfiguracaoDto> ConfiguracaoAsync(FonteEstatisticaExterna fonte, CancellationToken ct = default);

    Task<OperadoresExternosConfiguracaoDto> SalvarHabilitadosAsync(
        FonteEstatisticaExterna fonte, IReadOnlyList<string>? nomes, CancellationToken ct = default);

    /// <summary>A equipe (nomes habilitados) no período, comparada com o período anterior de mesmo tamanho.</summary>
    Task<EquipeExternaEstatisticaDto> EquipeAsync(
        FonteEstatisticaExterna fonte, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<EquipeExternaEstatisticaDto> EquipeAsync(
        FonteEstatisticaExterna fonte, DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados,
        CancellationToken ct = default);

    /// <summary>Uma pessoa (chave do ranking) no período.</summary>
    Task<IndividualExternoEstatisticaDto> IndividualAsync(
        FonteEstatisticaExterna fonte, string chave, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IndividualExternoEstatisticaDto> IndividualAsync(
        FonteEstatisticaExterna fonte, string chave, DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados,
        CancellationToken ct = default);
}

/// <summary>
/// Estatísticas do trabalho dos operadores no SER e no SERNIT, a partir da trilha "Histórico da
/// Solicitação" que a varredura captura (<c>ser_evento</c>/<c>sernit_evento</c>): cada evento traz
/// <b>quem</b> fez (<c>usuario</c>, nome livre do sistema externo), <b>quando</b> (com hora — ao
/// contrário do SISREG) e o <b>verbo</b> já tipado (<see cref="TipoEventoExterno"/>).
///
/// <para><b>Mesmo desenho das estatísticas do SISREG</b> (<see cref="EstatisticasOperadoresService"/>):
/// uma leitura por pedido para uma tabela temporária (atual + anterior, todos os nomes; os não
/// habilitados ficam com chave nula e só entram no total geral), agregações dela, 5 minutos de
/// cache. A lista de quem entra mora no <c>ParametrosJson</c> da credencial <c>ser</c>/<c>sernit</c>,
/// sob a mesma chave que o SISREG usa (<see cref="OperadoresDaEstatistica.Chave"/>).</para>
///
/// <para><b>O que é "ação".</b> Todo evento assinado pelo nome — agendar, reagendar, cancelar,
/// pendenciar, FollowUP, chegada, transferência… Os cartões separam os verbos que importam para a
/// regulação (agendamentos, cancelamentos, FollowUPs, pendências); o resto conta só no total.
/// A espera é medida no agendamento: dias entre a data da solicitação e o dia em que a pessoa a
/// agendou. Sem valor financeiro — o SER não traz tabela de preço.</para>
/// </summary>
public sealed class EstatisticasOperadoresExternosService(
    SmsMaisDbContext db,
    IIntegracaoCredencialService credenciais,
    IMemoryCache cache) : IEstatisticasOperadoresExternosService
{
    /// <summary>Mesmo teto das estatísticas do SISREG: comparar anos é outra pergunta.</summary>
    public const int MaxDiasPeriodo = 366;

    private static readonly TimeSpan TtlConsulta = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TtlListaDeNomes = TimeSpan.FromMinutes(10);
    private static readonly string[] RotulosDia = ["", "seg", "ter", "qua", "qui", "sex", "sáb", "dom"];

    /// <summary>Brasília é UTC−3 fixo (sem horário de verão desde 2019). <c>data_evento</c> é timestamptz.</summary>
    private const int OffsetHorasBrasilia = 3;

    private const string CondAgendamento = "tipo IN (5, 10)"; // Agendar, Reagendar
    private const string CondCancelamento = "tipo = 4";       // Cancelar
    private const string CondFollowUp = "tipo = 2";           // FollowUp
    private const string CondPendencia = "tipo = 3";          // Pendenciar

    // ------------------------------------------------------------------ configuração

    public async Task<OperadoresExternosConfiguracaoDto> ConfiguracaoAsync(
        FonteEstatisticaExterna fonte, CancellationToken ct = default)
    {
        var habilitados = await OperadoresDaEstatistica.ObterAsync(credenciais, fonte.Provedor(), ct);
        return await MontarConfiguracaoAsync(fonte, habilitados, ct);
    }

    public async Task<OperadoresExternosConfiguracaoDto> SalvarHabilitadosAsync(
        FonteEstatisticaExterna fonte, IReadOnlyList<string>? nomes, CancellationToken ct = default)
    {
        var habilitados = await OperadoresDaEstatistica.SalvarAsync(credenciais, fonte.Provedor(), nomes, ct);
        return await MontarConfiguracaoAsync(fonte, habilitados, ct);
    }

    private async Task<OperadoresExternosConfiguracaoDto> MontarConfiguracaoAsync(
        FonteEstatisticaExterna fonte, IReadOnlySet<string> habilitados, CancellationToken ct)
    {
        var nomes = await cache.GetOrCreateAsync($"est-externos:{fonte}:nomes", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TtlListaDeNomes;
            return await ListarNomesAsync(fonte, ct);
        }) ?? [];

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var saida = new List<OperadorExternoDto>(nomes.Count + habilitados.Count);

        foreach (var n in nomes)
        {
            vistos.Add(n.Nome);
            saida.Add(new OperadorExternoDto(
                n.Nome, n.Acoes, n.Primeira, n.Ultima, n.Lotacao, habilitados.Contains(n.Nome)));
        }

        // Habilitado que não aparece mais no histórico: continua visível para poder ser desmarcado.
        foreach (var h in habilitados.Where(h => !vistos.Contains(h)))
        {
            saida.Add(new OperadorExternoDto(h, 0, null, null, null, true));
        }

        return new OperadoresExternosConfiguracaoDto(saida, habilitados.Count);
    }

    private async Task<List<NomeHistorico>> ListarNomesAsync(FonteEstatisticaExterna fonte, CancellationToken ct)
    {
        var p = fonte.PrefixoTabela();
        var sql = $"""
            WITH x AS (
              SELECT upper(trim(e.usuario)) AS nome,
                     (e.data_evento AT TIME ZONE 'America/Sao_Paulo')::date AS d,
                     nullif(trim(e.lotacao_evento), '') AS lot
              FROM smsmarica.{p}_evento e
              WHERE e.usuario IS NOT NULL AND trim(e.usuario) <> ''
            ), lotacao AS (
              SELECT DISTINCT ON (nome) nome, lot
              FROM x WHERE lot IS NOT NULL
              GROUP BY nome, lot
              ORDER BY nome, count(*) DESC, lot
            )
            SELECT x.nome, count(*)::int, min(x.d), max(x.d), l.lot
            FROM x
            LEFT JOIN lotacao l ON l.nome = x.nome
            GROUP BY x.nome, l.lot
            ORDER BY 2 DESC, 1
            """;

        return await ComConexaoAsync(async (conn, tx) =>
        {
            await using var cmd = Comando(conn, tx, sql);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<NomeHistorico>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new NomeHistorico(
                    r.GetString(0), r.GetInt32(1),
                    r.IsDBNull(2) ? null : r.GetFieldValue<DateOnly>(2),
                    r.IsDBNull(3) ? null : r.GetFieldValue<DateOnly>(3),
                    r.IsDBNull(4) ? null : r.GetString(4)));
            }
            return lista;
        }, ct);
    }

    // ------------------------------------------------------------------ equipe

    public async Task<EquipeExternaEstatisticaDto> EquipeAsync(
        FonteEstatisticaExterna fonte, DateOnly de, DateOnly ate, CancellationToken ct = default) =>
        await EquipeAsync(fonte, de, ate, await OperadoresDaEstatistica.ObterAsync(credenciais, fonte.Provedor(), ct), ct);

    public async Task<EquipeExternaEstatisticaDto> EquipeAsync(
        FonteEstatisticaExterna fonte, DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados,
        CancellationToken ct = default)
    {
        var dias = ValidarPeriodo(de, ate);
        var mapa = MontarMapa(habilitados);
        var c = await ConsultarComCacheAsync(fonte, "equipe", mapa, de, ate, ct);

        var diasUteis = c.PorDia.Where(d => d.Dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        var top3 = c.Ranking.Take(3).Sum(o => o.Acoes);

        return new EquipeExternaEstatisticaDto(
            fonte.Rotulo(),
            dias,
            habilitados.Count,
            c.Atual,
            c.Anterior,
            c.AcoesTodosOsOperadores,
            diasUteis.Count == 0 ? 0 : Math.Round(diasUteis.Average(d => d.Operadores), 1),
            c.Atual.Acoes == 0 ? 0 : Math.Round(100.0 * top3 / c.Atual.Acoes, 1),
            c.PorDia.OrderByDescending(d => d.Acoes).ThenBy(d => d.Dia).FirstOrDefault(),
            c.PorDia,
            c.PorMes,
            c.PorDiaSemana,
            c.PorHora,
            c.Ranking,
            c.PorTipoEvento,
            c.TopRecursos,
            c.TopUnidadesExecutoras,
            c.TopLotacoes);
    }

    // ------------------------------------------------------------------ individual

    public async Task<IndividualExternoEstatisticaDto> IndividualAsync(
        FonteEstatisticaExterna fonte, string chave, DateOnly de, DateOnly ate, CancellationToken ct = default) =>
        await IndividualAsync(
            fonte, chave, de, ate, await OperadoresDaEstatistica.ObterAsync(credenciais, fonte.Provedor(), ct), ct);

    public async Task<IndividualExternoEstatisticaDto> IndividualAsync(
        FonteEstatisticaExterna fonte, string chave, DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados,
        CancellationToken ct = default)
    {
        var dias = ValidarPeriodo(de, ate);
        var chaveNormalizada = (chave ?? string.Empty).Trim();

        var daPessoa = MontarMapa(habilitados)
            .Where(m => string.Equals(m.Chave, chaveNormalizada, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (daPessoa.Count == 0)
        {
            // Fora dos habilitados não é "não existe": a configuração decide quem aparece.
            throw new NaoEncontradoException($"Operador habilitado nas estatísticas do {fonte.Rotulo()}", chaveNormalizada);
        }

        var c = await ConsultarComCacheAsync(fonte, "individual", daPessoa, de, ate, ct);
        var p = daPessoa[0];

        return new IndividualExternoEstatisticaDto(
            fonte.Rotulo(),
            p.Chave,
            p.Nome,
            dias,
            c.Ranking.FirstOrDefault(),
            c.Atual,
            c.Anterior,
            c.PorDia,
            c.PorMes,
            c.PorDiaSemana,
            c.PorHora,
            c.PorTipoEvento,
            c.TopRecursos,
            c.TopUnidadesExecutoras,
            c.TopLotacoes);
    }

    // ------------------------------------------------------------------ consulta

    private static int ValidarPeriodo(DateOnly de, DateOnly ate)
    {
        if (ate < de)
        {
            throw new ValidacaoException("estatisticas.periodo_invertido", "A data final é anterior à inicial.");
        }

        var dias = ate.DayNumber - de.DayNumber + 1;
        if (dias > MaxDiasPeriodo)
        {
            throw new ValidacaoException(
                "estatisticas.periodo_longo", $"Escolha um período de até {MaxDiasPeriodo} dias.");
        }

        return dias;
    }

    /// <summary>Nome → pessoa. No SER/SERNIT o nome é a pessoa: não há login para associar a usuário nosso.</summary>
    private static List<ItemMapa> MontarMapa(IReadOnlySet<string> habilitados) =>
    [
        .. habilitados
            .Select(OperadoresDaEstatistica.Normalizar)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(n => new ItemMapa(n, $"o:{n}")),
    ];

    private async Task<Consulta> ConsultarComCacheAsync(
        FonteEstatisticaExterna fonte, string escopo, IReadOnlyList<ItemMapa> mapa, DateOnly de, DateOnly ate,
        CancellationToken ct)
    {
        var chave = $"est-externos:{fonte}:{escopo}:{de:yyyyMMdd}:{ate:yyyyMMdd}:"
                    + string.Join(',', mapa.Select(m => m.Nome));
        return (await cache.GetOrCreateAsync(chave, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TtlConsulta;
            return await ConsultarAsync(fonte, mapa, de, ate, ct);
        }))!;
    }

    private async Task<Consulta> ConsultarAsync(
        FonteEstatisticaExterna fonte, IReadOnlyList<ItemMapa> mapa, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var dias = ate.DayNumber - de.DayNumber + 1;
        var anteriorAte = de.AddDays(-1);
        var anteriorDe = anteriorAte.AddDays(-(dias - 1));

        return await ComConexaoAsync(async (conn, tx) =>
        {
            // 1. A leitura única: atual + anterior, TODOS os nomes (os não habilitados ficam com chave
            //    nula — só entram no total geral). O recorte é feito em UTC (bounds fixos, usa índice)
            //    e o dia/hora são convertidos para Brasília já na carga.
            await using (var carga = Comando(conn, tx, SqlCarga(fonte.PrefixoTabela())))
            {
                carga.Parameters.AddWithValue("nomes", mapa.Select(m => m.Nome).ToArray());
                carga.Parameters.AddWithValue("chaves", mapa.Select(m => m.Chave).ToArray());
                carga.Parameters.AddWithValue("inicioUtc", InicioDoDiaUtc(anteriorDe));
                carga.Parameters.AddWithValue("fimUtc", InicioDoDiaUtc(ate.AddDays(1)));
                carga.Parameters.AddWithValue("de", de);
                await carga.ExecuteNonQueryAsync(ct);
            }

            var (atual, anterior, todos) = await LerResumoAsync(conn, tx, de, ate, anteriorDe, anteriorAte, dias, ct);
            var porDia = await LerSerieDiaAsync(conn, tx, ct);
            var porMes = await LerSerieMesAsync(conn, tx, ct);
            var porDiaSemana = await LerDiaSemanaAsync(conn, tx, ct);
            var porHora = await LerHoraAsync(conn, tx, ct);
            var ranking = await LerRankingAsync(conn, tx, mapa, dias, ct);
            var porTipo = await LerPorTipoAsync(conn, tx, ct);
            var recursos = await LerTopAsync(conn, tx, "b.recurso", ct);
            var executoras = await LerTopAsync(conn, tx, "b.ue", ct);
            var lotacoes = await LerTopAsync(conn, tx, "b.lot", ct);

            return new Consulta(
                atual, anterior, todos, porDia, porMes, porDiaSemana, porHora, ranking, porTipo, recursos, executoras, lotacoes);
        }, ct);
    }

    /// <summary>00:00 de Brasília do dia, em UTC (Kind=Utc, como o Npgsql exige para timestamptz).</summary>
    private static DateTime InicioDoDiaUtc(DateOnly dia) =>
        DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddHours(OffsetHorasBrasilia);

    private static string SqlCarga(string p) => $"""
        CREATE TEMP TABLE est_ext ON COMMIT DROP AS
        SELECT m.chave, x.nome, (x.d >= @de) AS atual, x.d, x.hora, x.hm, x.tipo, x.sol, x.ds, x.recurso, x.ue, x.lot
        FROM (
          SELECT upper(trim(e.usuario)) AS nome,
                 (e.data_evento AT TIME ZONE 'America/Sao_Paulo')::date AS d,
                 extract(hour FROM (e.data_evento AT TIME ZONE 'America/Sao_Paulo'))::int AS hora,
                 (extract(hour FROM (e.data_evento AT TIME ZONE 'America/Sao_Paulo'))
                    + extract(minute FROM (e.data_evento AT TIME ZONE 'America/Sao_Paulo')) / 60.0) AS hm,
                 e.tipo_evento AS tipo,
                 e.{p}_solicitacao_id AS sol,
                 s.data_solicitacao AS ds,
                 coalesce(nullif(trim(s.recurso), ''), '(sem recurso)') AS recurso,
                 coalesce(nullif(trim(e.unidade_executora), ''), nullif(trim(s.unidade_executora), ''), '(sem unidade)') AS ue,
                 coalesce(nullif(trim(e.lotacao_evento), ''), '(sem lotação)') AS lot
          FROM smsmarica.{p}_evento e
          JOIN smsmarica.{p}_solicitacao s ON s.id = e.{p}_solicitacao_id
          WHERE e.usuario IS NOT NULL AND trim(e.usuario) <> ''
            AND e.data_evento >= @inicioUtc AND e.data_evento < @fimUtc
        ) x
        LEFT JOIN unnest(@nomes::text[], @chaves::text[]) AS m(nome, chave) ON m.nome = x.nome
        """;

    private static async Task<(ResumoPeriodoExternoDto Atual, ResumoPeriodoExternoDto Anterior, int Todos)> LerResumoAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, DateOnly de, DateOnly ate, DateOnly anteriorDe, DateOnly anteriorAte,
        int dias, CancellationToken ct)
    {
        var sql = $"""
            SELECT
              count(*) FILTER (WHERE atual AND chave IS NOT NULL)::int,
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND {CondAgendamento})::int,
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND {CondCancelamento})::int,
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND {CondFollowUp})::int,
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND {CondPendencia})::int,
              count(DISTINCT sol) FILTER (WHERE atual AND chave IS NOT NULL)::int,
              count(DISTINCT chave) FILTER (WHERE atual)::int,
              count(DISTINCT d) FILTER (WHERE atual AND chave IS NOT NULL)::int,
              percentile_cont(0.5) WITHIN GROUP (ORDER BY d - ds)
                FILTER (WHERE atual AND chave IS NOT NULL AND ds IS NOT NULL AND {CondAgendamento}),
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND extract(isodow FROM d) >= 6)::int,
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND (hora < 7 OR hora >= 19))::int,
              count(*) FILTER (WHERE atual)::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL)::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND {CondAgendamento})::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND {CondCancelamento})::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND {CondFollowUp})::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND {CondPendencia})::int,
              count(DISTINCT sol) FILTER (WHERE NOT atual AND chave IS NOT NULL)::int,
              count(DISTINCT chave) FILTER (WHERE NOT atual)::int,
              count(DISTINCT d) FILTER (WHERE NOT atual AND chave IS NOT NULL)::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND extract(isodow FROM d) >= 6)::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND (hora < 7 OR hora >= 19))::int
            FROM est_ext
            """;

        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);

        var atual = Resumo(de, ate, dias,
            r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6),
            r.GetInt32(7), Dbl(r, 8), r.GetInt32(9), r.GetInt32(10));
        var anterior = Resumo(anteriorDe, anteriorAte, dias,
            r.GetInt32(12), r.GetInt32(13), r.GetInt32(14), r.GetInt32(15), r.GetInt32(16), r.GetInt32(17), r.GetInt32(18),
            r.GetInt32(19), null, r.GetInt32(20), r.GetInt32(21));
        return (atual, anterior, r.GetInt32(11));
    }

    private static ResumoPeriodoExternoDto Resumo(
        DateOnly de, DateOnly ate, int dias, int total, int agendamentos, int cancelamentos, int followUps, int pendencias,
        int solicitacoes, int operadores, int diasAtivos, double? espera, int fimDeSemana, int foraExpediente) =>
        new(de, ate, total, agendamentos, cancelamentos, followUps, pendencias, solicitacoes, operadores, diasAtivos,
            Math.Round((double)total / dias, 1),
            diasAtivos == 0 ? 0 : Math.Round((double)total / diasAtivos, 1),
            espera,
            total == 0 ? 0 : Math.Round(100.0 * fimDeSemana / total, 1),
            total == 0 ? 0 : Math.Round(100.0 * foraExpediente / total, 1));

    private static async Task<List<SerieDiaExternoDto>> LerSerieDiaAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var sql = $"""
            SELECT d, count(*)::int, count(DISTINCT chave)::int, count(*) FILTER (WHERE {CondAgendamento})::int
            FROM est_ext WHERE atual AND chave IS NOT NULL
            GROUP BY d ORDER BY d
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<SerieDiaExternoDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new SerieDiaExternoDto(r.GetFieldValue<DateOnly>(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3)));
        }
        return lista;
    }

    private static async Task<List<SerieMesExternoDto>> LerSerieMesAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var sql = $"""
            SELECT date_trunc('month', d)::date, count(*)::int, count(DISTINCT chave)::int,
                   count(*) FILTER (WHERE {CondAgendamento})::int
            FROM est_ext WHERE atual AND chave IS NOT NULL
            GROUP BY 1 ORDER BY 1
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<SerieMesExternoDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new SerieMesExternoDto(r.GetFieldValue<DateOnly>(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3)));
        }
        return lista;
    }

    private static async Task<List<DiaSemanaExternoDto>> LerDiaSemanaAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        const string sql = """
            SELECT extract(isodow FROM d)::int, count(*)::int, count(DISTINCT d)::int
            FROM est_ext WHERE atual AND chave IS NOT NULL
            GROUP BY 1
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var porDia = new Dictionary<int, (int Total, int Dias)>();
        while (await r.ReadAsync(ct))
        {
            porDia[r.GetInt32(0)] = (r.GetInt32(1), r.GetInt32(2));
        }

        // Os sete dias sempre, na ordem da semana: dia sem trabalho é informação.
        return
        [
            .. Enumerable.Range(1, 7).Select(i =>
            {
                var (total, dias) = porDia.GetValueOrDefault(i);
                return new DiaSemanaExternoDto(i, RotulosDia[i], total, dias, dias == 0 ? 0 : Math.Round((double)total / dias, 1));
            }),
        ];
    }

    private static async Task<List<HoraExternoDto>> LerHoraAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var sql = $"""
            SELECT hora, count(*)::int, count(*) FILTER (WHERE {CondAgendamento})::int
            FROM est_ext WHERE atual AND chave IS NOT NULL
            GROUP BY 1
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var porHora = new Dictionary<int, (int Total, int Agendamentos)>();
        while (await r.ReadAsync(ct))
        {
            porHora[r.GetInt32(0)] = (r.GetInt32(1), r.GetInt32(2));
        }

        // As 24 horas sempre: hora vazia é informação (ninguém de madrugada).
        return
        [
            .. Enumerable.Range(0, 24).Select(h =>
            {
                var (total, agendamentos) = porHora.GetValueOrDefault(h);
                return new HoraExternoDto(h, total, agendamentos);
            }),
        ];
    }

    private static async Task<List<OperadorExternoPeriodoDto>> LerRankingAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, IReadOnlyList<ItemMapa> mapa, int dias, CancellationToken ct)
    {
        var sql = $"""
            WITH pd AS (
              SELECT chave, d, count(*) AS n, min(hm) AS ini, max(hm) AS fim
              FROM est_ext WHERE atual AND chave IS NOT NULL GROUP BY 1, 2
            ), pico AS (
              SELECT DISTINCT ON (chave) chave, n, d FROM pd ORDER BY chave, n DESC, d
            ), jornada AS (
              SELECT chave,
                     percentile_cont(0.5) WITHIN GROUP (ORDER BY ini) AS ini,
                     percentile_cont(0.5) WITHIN GROUP (ORDER BY fim) AS fim
              FROM pd GROUP BY chave
            )
            SELECT b.chave,
                   count(*)::int,
                   count(*) FILTER (WHERE {CondAgendamento})::int,
                   count(*) FILTER (WHERE {CondCancelamento})::int,
                   count(*) FILTER (WHERE {CondFollowUp})::int,
                   count(*) FILTER (WHERE {CondPendencia})::int,
                   count(DISTINCT b.sol)::int,
                   count(DISTINCT b.d)::int,
                   p.n::int,
                   p.d,
                   count(*) FILTER (WHERE extract(isodow FROM b.d) >= 6)::int,
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY b.d - b.ds) FILTER (WHERE b.ds IS NOT NULL AND {CondAgendamento}),
                   percentile_cont(0.9) WITHIN GROUP (ORDER BY b.d - b.ds) FILTER (WHERE b.ds IS NOT NULL AND {CondAgendamento}),
                   j.ini,
                   j.fim,
                   count(DISTINCT b.recurso)::int,
                   count(DISTINCT b.ue)::int,
                   array_agg(DISTINCT b.lot)
            FROM est_ext b
            JOIN pico p ON p.chave = b.chave
            JOIN jornada j ON j.chave = b.chave
            WHERE b.atual AND b.chave IS NOT NULL
            GROUP BY b.chave, p.n, p.d, j.ini, j.fim
            """;

        var porChave = mapa.GroupBy(m => m.Chave).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var lista = new List<OperadorExternoPeriodoDto>();
        await using (var cmd = Comando(conn, tx, sql))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var chave = r.GetString(0);
                var pessoa = porChave.GetValueOrDefault(chave);
                var total = r.GetInt32(1);
                var diasTrab = r.GetInt32(7);

                lista.Add(new OperadorExternoPeriodoDto(
                    chave,
                    pessoa?.Nome ?? chave,
                    total,
                    r.GetInt32(2),
                    r.GetInt32(3),
                    r.GetInt32(4),
                    r.GetInt32(5),
                    r.GetInt32(6),
                    diasTrab,
                    dias,
                    Math.Round((double)total / dias, 1),
                    diasTrab == 0 ? 0 : Math.Round((double)total / diasTrab, 1),
                    r.GetInt32(8),
                    r.IsDBNull(9) ? null : r.GetFieldValue<DateOnly>(9),
                    total == 0 ? 0 : Math.Round(100.0 * r.GetInt32(10) / total, 1),
                    Dbl(r, 11),
                    Dbl(r, 12),
                    Dbl(r, 13),
                    Dbl(r, 14),
                    r.GetInt32(15),
                    r.GetInt32(16),
                    [.. r.GetFieldValue<string[]>(17).Order(StringComparer.Ordinal)]));
            }
        }

        return [.. lista.OrderByDescending(o => o.Acoes).ThenBy(o => o.Nome, StringComparer.Ordinal)];
    }

    private static async Task<List<TopItemExternoDto>> LerPorTipoAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        const string sql = """
            SELECT tipo, count(*)::int, count(DISTINCT chave)::int
            FROM est_ext WHERE atual AND chave IS NOT NULL
            GROUP BY 1
            ORDER BY 2 DESC, 1
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<TopItemExternoDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new TopItemExternoDto(RotuloTipo((TipoEventoExterno)r.GetInt32(0)), r.GetInt32(1), r.GetInt32(2)));
        }
        return lista;
    }

    /// <summary>O verbo em português de tela. Verbo novo do sistema externo cai em "Outro" já na captura.</summary>
    public static string RotuloTipo(TipoEventoExterno tipo) => tipo switch
    {
        TipoEventoExterno.Solicitar => "Solicitação",
        TipoEventoExterno.FollowUp => "FollowUP",
        TipoEventoExterno.Pendenciar => "Pendência",
        TipoEventoExterno.Cancelar => "Cancelamento",
        TipoEventoExterno.Agendar => "Agendamento",
        TipoEventoExterno.ChegadaNoDestino => "Chegada no destino",
        TipoEventoExterno.Transferir => "Transferência",
        TipoEventoExterno.DevolvidoParaRegulacao => "Devolvido à regulação",
        TipoEventoExterno.WhatsApp => "WhatsApp",
        TipoEventoExterno.Reagendar => "Reagendamento",
        TipoEventoExterno.RetornarParaFila => "Retorno à fila",
        TipoEventoExterno.Alta => "Alta",
        TipoEventoExterno.CorrigirDados => "Correção de dados",
        _ => "Outro",
    };

    private static async Task<List<TopItemExternoDto>> LerTopAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string rotulo, CancellationToken ct)
    {
        var sql = $"""
            SELECT {rotulo}, count(*)::int, count(DISTINCT b.chave)::int
            FROM est_ext b
            WHERE b.atual AND b.chave IS NOT NULL
            GROUP BY 1
            ORDER BY 2 DESC, 1
            LIMIT 15
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<TopItemExternoDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new TopItemExternoDto(r.GetString(0), r.GetInt32(1), r.GetInt32(2)));
        }
        return lista;
    }

    // ------------------------------------------------------------------ infraestrutura

    private async Task<T> ComConexaoAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> acao, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        var abriu = conn.State != ConnectionState.Open;
        if (abriu) await conn.OpenAsync(ct);
        try
        {
            // Transação: a tabela temporária morre no fim dela (ON COMMIT DROP), mesmo com pool.
            await using var tx = await conn.BeginTransactionAsync(ct);
            var resultado = await acao(conn, tx);
            await tx.CommitAsync(ct);
            return resultado;
        }
        finally
        {
            if (abriu) await conn.CloseAsync();
        }
    }

    private static NpgsqlCommand Comando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql) =>
        new(sql, conn, tx) { CommandTimeout = 120 };

    private static double? Dbl(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : Math.Round(r.GetDouble(i), 1);

    private sealed record NomeHistorico(string Nome, int Acoes, DateOnly? Primeira, DateOnly? Ultima, string? Lotacao);

    private sealed record ItemMapa(string Nome, string Chave);

    private sealed record Consulta(
        ResumoPeriodoExternoDto Atual,
        ResumoPeriodoExternoDto Anterior,
        int AcoesTodosOsOperadores,
        IReadOnlyList<SerieDiaExternoDto> PorDia,
        IReadOnlyList<SerieMesExternoDto> PorMes,
        IReadOnlyList<DiaSemanaExternoDto> PorDiaSemana,
        IReadOnlyList<HoraExternoDto> PorHora,
        IReadOnlyList<OperadorExternoPeriodoDto> Ranking,
        IReadOnlyList<TopItemExternoDto> PorTipoEvento,
        IReadOnlyList<TopItemExternoDto> TopRecursos,
        IReadOnlyList<TopItemExternoDto> TopUnidadesExecutoras,
        IReadOnlyList<TopItemExternoDto> TopLotacoes);
}
