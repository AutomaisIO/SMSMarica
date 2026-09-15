using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.SisregWeb.Estatisticas.Dtos;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Estatisticas;

public interface IEstatisticasOperadoresService
{
    /// <summary>Todos os logins que já autorizaram algo, com a marca de quem entra nas estatísticas.</summary>
    Task<OperadoresConfiguracaoDto> ConfiguracaoAsync(CancellationToken ct = default);

    Task<OperadoresConfiguracaoDto> SalvarHabilitadosAsync(IReadOnlyList<string>? logins, CancellationToken ct = default);

    /// <summary>A equipe (logins habilitados) no período, comparada com o período anterior de mesmo tamanho.</summary>
    Task<EquipeEstatisticaDto> EquipeAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<EquipeEstatisticaDto> EquipeAsync(
        DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados, CancellationToken ct = default);

    /// <summary>Uma pessoa (chave do ranking) no período.</summary>
    Task<IndividualEstatisticaDto> IndividualAsync(
        string chave, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IndividualEstatisticaDto> IndividualAsync(
        string chave, DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados, CancellationToken ct = default);
}

/// <summary>
/// Estatísticas do trabalho dos operadores da regulação, a partir do export da agenda do SISREG que
/// já importamos: cada agendamento traz <c>operador_autorizador</c> (coluna 33) e
/// <c>data_autorizacao</c> (vira <c>solicitacao.data_regulacao</c>).
///
/// <para><b>Só entra o que é seguro</b> (decidido em 15/09/2026, depois de medir em produção):
/// volume, valor, ritmo (dias trabalhados × corridos), tempos em DIAS (a autorização não tem hora),
/// amplitude e rankings. Ficaram de fora o comparecimento (quem confirma é a unidade executante, não
/// o regulador) e remarcação/cancelamento posterior (dado só desde 06/09/2026).</para>
///
/// <para><b>Uma leitura por pedido.</b> A tabela <c>solicitacao</c> tem ~1 milhão de linhas e o
/// login mora dentro do texto cru; ler uma vez o período atual + o anterior para uma tabela
/// temporária e agregar dela custa o mesmo que uma consulta só (medido: 30 dias em 2,5 s, 9 meses em
/// 9,6 s). O resultado fica 5 minutos em cache.</para>
/// </summary>
public sealed class EstatisticasOperadoresService(
    SmsMaisDbContext db,
    IIntegracaoCredencialService credenciais,
    IMemoryCache cache) : IEstatisticasOperadoresService
{
    /// <summary>Mais que isto a leitura passa de ~20 s — e comparar anos é outra pergunta.</summary>
    public const int MaxDiasPeriodo = 366;

    private static readonly TimeSpan TtlConsulta = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TtlListaDeLogins = TimeSpan.FromMinutes(10);
    private static readonly string[] RotulosDia = ["", "seg", "ter", "qua", "qui", "sex", "sáb", "dom"];

    // ------------------------------------------------------------------ configuração

    public async Task<OperadoresConfiguracaoDto> ConfiguracaoAsync(CancellationToken ct = default)
    {
        var habilitados = await OperadoresDaEstatistica.ObterAsync(credenciais, ct);
        return await MontarConfiguracaoAsync(habilitados, ct);
    }

    public async Task<OperadoresConfiguracaoDto> SalvarHabilitadosAsync(
        IReadOnlyList<string>? logins, CancellationToken ct = default)
    {
        var habilitados = await OperadoresDaEstatistica.SalvarAsync(credenciais, logins, ct);
        return await MontarConfiguracaoAsync(habilitados, ct);
    }

    private async Task<OperadoresConfiguracaoDto> MontarConfiguracaoAsync(
        IReadOnlySet<string> habilitados, CancellationToken ct)
    {
        var logins = await cache.GetOrCreateAsync("est-operadores:logins", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TtlListaDeLogins;
            return await ListarLoginsAsync(ct);
        }) ?? [];

        var pessoas = await PessoasPorLoginAsync(ct);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var saida = new List<OperadorSisregDto>(logins.Count + habilitados.Count);

        foreach (var l in logins)
        {
            vistos.Add(l.Login);
            var p = pessoas.GetValueOrDefault(l.Login);
            saida.Add(new OperadorSisregDto(
                l.Login, l.Autorizacoes, l.Primeira, l.Ultima, habilitados.Contains(l.Login), p?.Id, p?.Nome));
        }

        // Habilitado que não aparece mais no histórico (lista importada mudou): continua visível para
        // poder ser desmarcado.
        foreach (var h in habilitados.Where(h => !vistos.Contains(h)))
        {
            var p = pessoas.GetValueOrDefault(h);
            saida.Add(new OperadorSisregDto(h, 0, null, null, true, p?.Id, p?.Nome));
        }

        return new OperadoresConfiguracaoDto(saida, habilitados.Count);
    }

    private async Task<List<LoginHistorico>> ListarLoginsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT x.login, count(*)::int, min(x.d), max(x.d)
            FROM (
              SELECT upper(trim(split_part(s.raw_sisreg, ';', 33))) AS login, s.data_regulacao AS d
              FROM smsmarica.solicitacao s
              WHERE s.excluido_em IS NULL AND s.raw_sisreg LIKE '%;%' AND s.data_regulacao IS NOT NULL
            ) x
            WHERE x.login <> ''
            GROUP BY x.login
            ORDER BY 2 DESC, 1
            """;

        return await ComConexaoAsync(async (conn, tx) =>
        {
            await using var cmd = Comando(conn, tx, sql);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var lista = new List<LoginHistorico>();
            while (await r.ReadAsync(ct))
            {
                lista.Add(new LoginHistorico(
                    r.GetString(0), r.GetInt32(1),
                    r.IsDBNull(2) ? null : r.GetFieldValue<DateOnly>(2),
                    r.IsDBNull(3) ? null : r.GetFieldValue<DateOnly>(3)));
            }
            return lista;
        }, ct);
    }

    // ------------------------------------------------------------------ equipe

    public async Task<EquipeEstatisticaDto> EquipeAsync(DateOnly de, DateOnly ate, CancellationToken ct = default) =>
        await EquipeAsync(de, ate, await OperadoresDaEstatistica.ObterAsync(credenciais, ct), ct);

    public async Task<EquipeEstatisticaDto> EquipeAsync(
        DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados, CancellationToken ct = default)
    {
        var dias = ValidarPeriodo(de, ate);
        var mapa = await MontarMapaAsync(habilitados, ct);
        var c = await ConsultarComCacheAsync("equipe", mapa, de, ate, ct);

        var diasUteis = c.PorDia.Where(d => d.Dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        var top3 = c.Ranking.Take(3).Sum(o => o.Autorizacoes);

        return new EquipeEstatisticaDto(
            dias,
            habilitados.Count,
            c.Atual,
            c.Anterior,
            c.AutorizacoesTodosOsLogins,
            diasUteis.Count == 0 ? 0 : Math.Round(diasUteis.Average(d => d.Operadores), 1),
            c.Atual.Autorizacoes == 0 ? 0 : Math.Round(100.0 * top3 / c.Atual.Autorizacoes, 1),
            c.PorDia.OrderByDescending(d => d.Autorizacoes).ThenBy(d => d.Dia).FirstOrDefault(),
            c.PorDia,
            c.PorMes,
            c.PorDiaSemana,
            c.Ranking,
            c.TopProcedimentos,
            c.TopUnidadesExecutantes,
            c.TopUnidadesSolicitantes);
    }

    // ------------------------------------------------------------------ individual

    public async Task<IndividualEstatisticaDto> IndividualAsync(
        string chave, DateOnly de, DateOnly ate, CancellationToken ct = default) =>
        await IndividualAsync(chave, de, ate, await OperadoresDaEstatistica.ObterAsync(credenciais, ct), ct);

    public async Task<IndividualEstatisticaDto> IndividualAsync(
        string chave, DateOnly de, DateOnly ate, IReadOnlySet<string> habilitados, CancellationToken ct = default)
    {
        var dias = ValidarPeriodo(de, ate);
        var chaveNormalizada = (chave ?? string.Empty).Trim();

        // Só os logins HABILITADOS da pessoa: o individual tem de bater com a linha dela no ranking.
        var daPessoa = (await MontarMapaAsync(habilitados, ct))
            .Where(m => string.Equals(m.Chave, chaveNormalizada, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (daPessoa.Count == 0)
        {
            // Fora dos habilitados não é "não existe": a configuração decide quem aparece.
            throw new NaoEncontradoException("Operador habilitado nas estatísticas", chaveNormalizada);
        }

        var c = await ConsultarComCacheAsync("individual", daPessoa, de, ate, ct);
        var p = daPessoa[0];

        return new IndividualEstatisticaDto(
            p.Chave,
            p.Nome,
            p.UsuarioId,
            [.. daPessoa.Select(m => m.Login).Order(StringComparer.Ordinal)],
            dias,
            c.Ranking.FirstOrDefault(),
            c.Atual,
            c.Anterior,
            c.PorDia,
            c.PorMes,
            c.PorDiaSemana,
            c.TopProcedimentos,
            c.TopUnidadesExecutantes,
            c.TopUnidadesSolicitantes);
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

    /// <summary>
    /// Login → pessoa. Login associado a um usuário vira a chave do usuário (soma os logins dele);
    /// sem associação, o próprio login é a pessoa.
    /// </summary>
    private async Task<List<ItemMapa>> MontarMapaAsync(IReadOnlySet<string> habilitados, CancellationToken ct)
    {
        var pessoas = await PessoasPorLoginAsync(ct);
        return
        [
            .. habilitados
                .Select(OperadoresDaEstatistica.Normalizar)
                .Where(l => l.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(l => pessoas.GetValueOrDefault(l) is { } p
                    ? new ItemMapa(l, $"u:{p.Id}", p.Nome, p.Id)
                    : new ItemMapa(l, $"l:{l}", l, null)),
        ];
    }

    private async Task<Dictionary<string, Pessoa>> PessoasPorLoginAsync(CancellationToken ct)
    {
        var usuarios = await db.Usuarios.AsNoTracking()
            .Where(u => u.ExcluidoEm == null && u.LoginsSisreg.Length > 0)
            .OrderBy(u => u.CriadoEm)
            .Select(u => new { u.Id, u.NomeCompleto, u.LoginsSisreg })
            .ToListAsync(ct);

        var porLogin = new Dictionary<string, Pessoa>(StringComparer.Ordinal);
        foreach (var u in usuarios)
        {
            foreach (var l in u.LoginsSisreg)
            {
                porLogin.TryAdd(OperadoresDaEstatistica.Normalizar(l), new Pessoa(u.Id, u.NomeCompleto));
            }
        }
        return porLogin;
    }

    private async Task<Consulta> ConsultarComCacheAsync(
        string escopo, IReadOnlyList<ItemMapa> mapa, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var chave = $"est-operadores:{escopo}:{de:yyyyMMdd}:{ate:yyyyMMdd}:"
                    + string.Join(',', mapa.Select(m => $"{m.Login}={m.Chave}"));
        return (await cache.GetOrCreateAsync(chave, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TtlConsulta;
            return await ConsultarAsync(mapa, de, ate, ct);
        }))!;
    }

    private async Task<Consulta> ConsultarAsync(
        IReadOnlyList<ItemMapa> mapa, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var dias = ate.DayNumber - de.DayNumber + 1;
        var anteriorAte = de.AddDays(-1);
        var anteriorDe = anteriorAte.AddDays(-(dias - 1));

        return await ComConexaoAsync(async (conn, tx) =>
        {
            // 1. A leitura única: atual + anterior, TODOS os logins (os não habilitados ficam com chave
            //    nula — só entram no total geral).
            await using (var carga = Comando(conn, tx, SqlCarga))
            {
                carga.Parameters.AddWithValue("logins", mapa.Select(m => m.Login).ToArray());
                carga.Parameters.AddWithValue("chaves", mapa.Select(m => m.Chave).ToArray());
                carga.Parameters.AddWithValue("inicio", anteriorDe);
                carga.Parameters.AddWithValue("de", de);
                carga.Parameters.AddWithValue("ate", ate);
                await carga.ExecuteNonQueryAsync(ct);
            }

            var (atual, anterior, todos) = await LerResumoAsync(conn, tx, de, ate, anteriorDe, anteriorAte, dias, ct);
            var porDia = await LerSerieDiaAsync(conn, tx, ct);
            var porMes = await LerSerieMesAsync(conn, tx, ct);
            var porDiaSemana = await LerDiaSemanaAsync(conn, tx, ct);
            var ranking = await LerRankingAsync(conn, tx, mapa, dias, ct);
            var procedimentos = await LerTopAsync(conn, tx, "b.proc", "", ct);
            var executantes = await LerTopAsync(
                conn, tx, "coalesce(u.nome, '(sem unidade)')", "LEFT JOIN smsmarica.unidade u ON u.id = b.ue", ct);
            var solicitantes = await LerTopAsync(
                conn, tx, "coalesce(u.nome, '(sem unidade)')", "LEFT JOIN smsmarica.unidade u ON u.id = b.us", ct);

            return new Consulta(
                atual, anterior, todos, porDia, porMes, porDiaSemana, ranking, procedimentos, executantes, solicitantes);
        }, ct);
    }

    private const string SqlCarga = """
        CREATE TEMP TABLE est_base ON COMMIT DROP AS
        SELECT m.chave, x.login, x.op_sol, (x.d >= @de) AS atual, x.d, x.ds, x.da, x.ue, x.us, x.pc, x.proc, x.valor
        FROM (
          SELECT upper(trim(split_part(s.raw_sisreg, ';', 33))) AS login,
                 upper(trim(split_part(s.raw_sisreg, ';', 31))) AS op_sol,
                 s.data_regulacao AS d,
                 s.data_solicitacao AS ds,
                 (s.data_agendada AT TIME ZONE 'America/Sao_Paulo')::date AS da,
                 s.unidade_executante_id AS ue,
                 s.unidade_solicitante_id AS us,
                 s.procedimento_codigo_sisreg AS pc,
                 coalesce(nullif(trim(s.procedimento_texto), ''), '(sem procedimento)') AS proc,
                 CASE WHEN replace(trim(split_part(s.raw_sisreg, ';', 34)), ',', '.') ~ '^[0-9]+(\.[0-9]+)?$'
                      THEN replace(trim(split_part(s.raw_sisreg, ';', 34)), ',', '.')::numeric END AS valor
          FROM smsmarica.solicitacao s
          WHERE s.excluido_em IS NULL
            AND s.raw_sisreg LIKE '%;%'
            AND s.data_regulacao BETWEEN @inicio AND @ate
        ) x
        LEFT JOIN unnest(@logins::text[], @chaves::text[]) AS m(login, chave) ON m.login = x.login
        WHERE x.login <> ''
        """;

    private static async Task<(ResumoPeriodoDto Atual, ResumoPeriodoDto Anterior, int Todos)> LerResumoAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, DateOnly de, DateOnly ate, DateOnly anteriorDe, DateOnly anteriorAte,
        int dias, CancellationToken ct)
    {
        const string sql = """
            SELECT
              count(*) FILTER (WHERE atual AND chave IS NOT NULL)::int,
              coalesce(sum(valor) FILTER (WHERE atual AND chave IS NOT NULL), 0),
              count(DISTINCT chave) FILTER (WHERE atual)::int,
              count(DISTINCT d) FILTER (WHERE atual AND chave IS NOT NULL)::int,
              percentile_cont(0.5) WITHIN GROUP (ORDER BY d - ds) FILTER (WHERE atual AND chave IS NOT NULL AND ds IS NOT NULL),
              percentile_cont(0.5) WITHIN GROUP (ORDER BY da - d) FILTER (WHERE atual AND chave IS NOT NULL AND da IS NOT NULL),
              count(*) FILTER (WHERE atual AND chave IS NOT NULL AND extract(isodow FROM d) >= 6)::int,
              count(*) FILTER (WHERE atual)::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL)::int,
              coalesce(sum(valor) FILTER (WHERE NOT atual AND chave IS NOT NULL), 0),
              count(DISTINCT chave) FILTER (WHERE NOT atual)::int,
              count(DISTINCT d) FILTER (WHERE NOT atual AND chave IS NOT NULL)::int,
              count(*) FILTER (WHERE NOT atual AND chave IS NOT NULL AND extract(isodow FROM d) >= 6)::int
            FROM est_base
            """;

        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);

        var atual = Resumo(de, ate, dias,
            r.GetInt32(0), r.GetDecimal(1), r.GetInt32(2), r.GetInt32(3), Dbl(r, 4), Dbl(r, 5), r.GetInt32(6));
        var anterior = Resumo(anteriorDe, anteriorAte, dias,
            r.GetInt32(8), r.GetDecimal(9), r.GetInt32(10), r.GetInt32(11), null, null, r.GetInt32(12));
        return (atual, anterior, r.GetInt32(7));
    }

    private static ResumoPeriodoDto Resumo(
        DateOnly de, DateOnly ate, int dias, int total, decimal valor, int operadores, int diasAtivos,
        double? espera, double? antecedencia, int fimDeSemana) =>
        new(de, ate, total, valor, operadores, diasAtivos,
            Math.Round((double)total / dias, 1),
            diasAtivos == 0 ? 0 : Math.Round((double)total / diasAtivos, 1),
            espera, antecedencia,
            total == 0 ? 0 : Math.Round(100.0 * fimDeSemana / total, 1));

    private static async Task<List<SerieDiaDto>> LerSerieDiaAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        const string sql = """
            SELECT d, count(*)::int, count(DISTINCT chave)::int, coalesce(sum(valor), 0)
            FROM est_base WHERE atual AND chave IS NOT NULL
            GROUP BY d ORDER BY d
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<SerieDiaDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new SerieDiaDto(r.GetFieldValue<DateOnly>(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3)));
        }
        return lista;
    }

    private static async Task<List<SerieMesDto>> LerSerieMesAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        const string sql = """
            SELECT date_trunc('month', d)::date, count(*)::int, count(DISTINCT chave)::int, coalesce(sum(valor), 0)
            FROM est_base WHERE atual AND chave IS NOT NULL
            GROUP BY 1 ORDER BY 1
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<SerieMesDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new SerieMesDto(r.GetFieldValue<DateOnly>(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3)));
        }
        return lista;
    }

    private static async Task<List<DiaSemanaDto>> LerDiaSemanaAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        const string sql = """
            SELECT extract(isodow FROM d)::int, count(*)::int, count(DISTINCT d)::int
            FROM est_base WHERE atual AND chave IS NOT NULL
            GROUP BY 1
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var porDia = new Dictionary<int, (int Total, int Dias)>();
        while (await r.ReadAsync(ct))
        {
            porDia[r.GetInt32(0)] = (r.GetInt32(1), r.GetInt32(2));
        }

        // Os sete dias sempre, na ordem da semana: dia sem trabalho é informação (ninguém no sábado).
        return
        [
            .. Enumerable.Range(1, 7).Select(i =>
            {
                var (total, dias) = porDia.GetValueOrDefault(i);
                return new DiaSemanaDto(i, RotulosDia[i], total, dias, dias == 0 ? 0 : Math.Round((double)total / dias, 1));
            }),
        ];
    }

    private static async Task<List<OperadorPeriodoDto>> LerRankingAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, IReadOnlyList<ItemMapa> mapa, int dias, CancellationToken ct)
    {
        const string sql = """
            WITH pd AS (
              SELECT chave, d, count(*) AS n FROM est_base WHERE atual AND chave IS NOT NULL GROUP BY 1, 2
            ), pico AS (
              SELECT DISTINCT ON (chave) chave, n, d FROM pd ORDER BY chave, n DESC, d
            )
            SELECT b.chave,
                   count(*)::int,
                   coalesce(sum(b.valor), 0),
                   count(DISTINCT b.d)::int,
                   p.n::int,
                   p.d,
                   count(*) FILTER (WHERE extract(isodow FROM b.d) >= 6)::int,
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY b.d - b.ds) FILTER (WHERE b.ds IS NOT NULL),
                   percentile_cont(0.9) WITHIN GROUP (ORDER BY b.d - b.ds) FILTER (WHERE b.ds IS NOT NULL),
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY b.da - b.d) FILTER (WHERE b.da IS NOT NULL),
                   count(DISTINCT b.ue)::int,
                   count(DISTINCT b.us)::int,
                   count(DISTINCT b.pc)::int,
                   count(*) FILTER (WHERE b.login = b.op_sol)::int,
                   array_agg(DISTINCT b.login)
            FROM est_base b
            JOIN pico p ON p.chave = b.chave
            WHERE b.atual AND b.chave IS NOT NULL
            GROUP BY b.chave, p.n, p.d
            """;

        // Rapidez em ocupar agenda nova: dias entre a ativação da escala (a que cobre o dia do
        // atendimento) e a autorização. Só escalas ativadas há até 90 dias — contra uma escala de
        // 2019 o número mediria a idade da agenda, não o operador.
        const string sqlAgendaNova = """
            SELECT b.chave,
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY b.d - e.ativada),
                   count(*)::int
            FROM est_base b
            CROSS JOIN LATERAL (
              SELECT max(es.ativada_em_sisreg) AS ativada
              FROM smsmarica.sisreg_escala es
              WHERE es.unidade_id = b.ue
                AND (es.procedimento_codigo = b.pc OR es.procedimento_codigo = substr(b.pc, 1, 4) || '000')
                AND es.dia_semana = extract(dow FROM b.da)::int
                AND b.da BETWEEN es.vigencia_inicio AND es.vigencia_fim
                AND es.ativada_em_sisreg <= b.d
            ) e
            WHERE b.atual AND b.chave IS NOT NULL AND b.pc IS NOT NULL AND b.da IS NOT NULL
              AND e.ativada IS NOT NULL AND b.d - e.ativada <= 90
            GROUP BY b.chave
            """;

        var agendaNova = new Dictionary<string, (double? Mediana, int N)>(StringComparer.Ordinal);
        await using (var cmd = Comando(conn, tx, sqlAgendaNova))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                agendaNova[r.GetString(0)] = (Dbl(r, 1), r.GetInt32(2));
            }
        }

        var porChave = mapa.GroupBy(m => m.Chave).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var lista = new List<OperadorPeriodoDto>();
        await using (var cmd = Comando(conn, tx, sql))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var chave = r.GetString(0);
                var pessoa = porChave.GetValueOrDefault(chave);
                var total = r.GetInt32(1);
                var diasTrab = r.GetInt32(3);
                var (mediana, n) = agendaNova.GetValueOrDefault(chave);

                lista.Add(new OperadorPeriodoDto(
                    chave,
                    pessoa?.Nome ?? chave,
                    pessoa?.UsuarioId,
                    [.. r.GetFieldValue<string[]>(14).Order(StringComparer.Ordinal)],
                    total,
                    r.GetDecimal(2),
                    diasTrab,
                    dias,
                    Math.Round((double)total / dias, 1),
                    diasTrab == 0 ? 0 : Math.Round((double)total / diasTrab, 1),
                    r.GetInt32(4),
                    r.IsDBNull(5) ? null : r.GetFieldValue<DateOnly>(5),
                    total == 0 ? 0 : Math.Round(100.0 * r.GetInt32(6) / total, 1),
                    Dbl(r, 7),
                    Dbl(r, 8),
                    Dbl(r, 9),
                    mediana,
                    n,
                    r.GetInt32(10),
                    r.GetInt32(11),
                    r.GetInt32(12),
                    total == 0 ? 0 : Math.Round(100.0 * r.GetInt32(13) / total, 1)));
            }
        }

        return [.. lista.OrderByDescending(o => o.Autorizacoes).ThenBy(o => o.Nome, StringComparer.Ordinal)];
    }

    private static async Task<List<TopItemDto>> LerTopAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string rotulo, string join, CancellationToken ct)
    {
        var sql = $"""
            SELECT {rotulo}, count(*)::int, coalesce(sum(b.valor), 0), count(DISTINCT b.chave)::int
            FROM est_base b {join}
            WHERE b.atual AND b.chave IS NOT NULL
            GROUP BY 1
            ORDER BY 2 DESC, 1
            LIMIT 15
            """;
        await using var cmd = Comando(conn, tx, sql);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<TopItemDto>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new TopItemDto(r.GetString(0), r.GetInt32(1), r.GetDecimal(2), r.GetInt32(3)));
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

    private sealed record LoginHistorico(string Login, int Autorizacoes, DateOnly? Primeira, DateOnly? Ultima);

    private sealed record Pessoa(Guid Id, string Nome);

    private sealed record ItemMapa(string Login, string Chave, string Nome, Guid? UsuarioId);

    private sealed record Consulta(
        ResumoPeriodoDto Atual,
        ResumoPeriodoDto Anterior,
        int AutorizacoesTodosOsLogins,
        IReadOnlyList<SerieDiaDto> PorDia,
        IReadOnlyList<SerieMesDto> PorMes,
        IReadOnlyList<DiaSemanaDto> PorDiaSemana,
        IReadOnlyList<OperadorPeriodoDto> Ranking,
        IReadOnlyList<TopItemDto> TopProcedimentos,
        IReadOnlyList<TopItemDto> TopUnidadesExecutantes,
        IReadOnlyList<TopItemDto> TopUnidadesSolicitantes);
}
