using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Data;

namespace SMSMais.Core.AgendaRegulacao;

public interface IAgendaDemandaService
{
    Task<DemandaResumoDto> ResumoAsync(DemandaFiltro filtro, CancellationToken ct = default);

    Task<IReadOnlyList<DemandaProcedimentoDto>> ProcedimentosAsync(
        DemandaFiltro filtro, string ordenarPor, int limite, CancellationToken ct = default);

    Task<IReadOnlyList<DemandaFaixaEsperaDto>> FaixasEsperaAsync(
        DemandaFiltro filtro, CancellationToken ct = default);

    Task<IReadOnlyList<DemandaOrigemDto>> OrigemAsync(
        DemandaFiltro filtro, string eixo, int limite, CancellationToken ct = default);

    Task<IReadOnlyList<DemandaSerieDto>> SerieAsync(DemandaFiltro filtro, CancellationToken ct = default);

    Task<DemandaOpcoesDto> OpcoesAsync(CancellationToken ct = default);

    Task<AgendaCoberturaDto> CoberturaAsync(CancellationToken ct = default);
}

/// <summary>
/// O <b>outro lado</b> da Agenda. Enquanto <see cref="AgendaAnaliseService"/> parte da escala
/// publicada — oferta —, aqui se parte da solicitação: o que a rede pediu, quem pediu, e
/// <b>quanto tempo levou até conseguir vaga</b>.
///
/// <para><b>Por que é um serviço separado, e não mais um eixo do outro.</b> A escala do SISREG é
/// publicada em <i>grupos</i> (código terminado em <c>000</c>) que se expandem em itens no
/// agendamento; cruzar oferta com ocupação por procedimento perde metade dos casos, e foi por isso
/// que o grão de lá é (unidade × profissional × dia), sem procedimento. Aqui não há cruzamento
/// nenhum — conta-se a própria solicitação —, então o procedimento volta a ser um eixo confiável.
/// Separar os dois é o que permite ter "top procedimentos" sem reintroduzir o descasamento
/// grupo × item onde ele faria estrago.</para>
///
/// <para><b>Espera é mediana, nunca média.</b> A distribuição tem cauda de anos (máximo medido em
/// 05/09/2026: 1.859 dias); a média descreveria um caso que não é o de ninguém. O p90 acompanha
/// como retrato da pior experiência real.</para>
///
/// <para><b>Tudo em dia de Brasília</b>, como no resto do módulo: <c>data_agendada</c> é
/// <c>timestamptz</c> e vira dia com <c>at time zone 'America/Sao_Paulo'</c>. Usar
/// <c>current_date</c> ou converter para UTC faria a mesma tela mostrar números diferentes à noite.</para>
/// </summary>
public sealed class AgendaDemandaService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IAgendaDemandaService
{
    /// <summary>Faixas fixas do histograma de espera — fixas para que duas consultas se comparem.</summary>
    private const string Faixas =
        "(values (1,'Até 7 dias',0,7),(2,'8 a 15 dias',8,15),(3,'16 a 30 dias',16,30)," +
        "(4,'31 a 60 dias',31,60),(5,'61 a 90 dias',61,90),(6,'91 a 180 dias',91,180)," +
        "(7,'Mais de 180 dias',181,1000000)) f(ordem, rotulo, lo, hi)";

    /// <summary>
    /// A definição única de "solicitação regulada no recorte". Todo endpoint parte daqui: uma
    /// definição só evita que dois painéis discordem sobre o mesmo número, que é o defeito clássico
    /// de tela montada consulta a consulta.
    ///
    /// <para><c>espera</c> já sai <b>nula quando negativa</b> — agendado antes de ter sido pedido é
    /// data errada na origem, não fila de tamanho negativo, e entraria nos percentis puxando a
    /// mediana para baixo. O caso continua contado à parte, em <c>espera_bruta</c>.</para>
    /// </summary>
    private const string Base = """
        with base as (
            select s.id,
                   coalesce(nullif(btrim(s.procedimento_texto), ''), '(sem procedimento)') proc,
                   s.unidade_executante_id ux,
                   s.unidade_solicitante_id us,
                   s.prioridade,
                   s.status_confirmacao,
                   (s.data_agendada at time zone 'America/Sao_Paulo')::date dag,
                   s.data_solicitacao dsol,
                   ((s.data_agendada at time zone 'America/Sao_Paulo')::date - s.data_solicitacao) espera_bruta,
                   nullif(greatest(
                       (s.data_agendada at time zone 'America/Sao_Paulo')::date - s.data_solicitacao, -1), -1) espera,
                   nullif(greatest(
                       (s.data_agendada at time zone 'America/Sao_Paulo')::date - s.data_regulacao, -1), -1) espera_reg
            from smsmarica.solicitacao s
            where s.excluido_em is null and s.cancelado_em is null
              and s.data_agendada is not null
              and (case when {2}::text = 'solicitada' then s.data_solicitacao
                        else (s.data_agendada at time zone 'America/Sao_Paulo')::date end)
                  between {0}::date and {1}::date
              and ({3}::uuid is null or s.unidade_executante_id = {3}::uuid)
              and ({4}::uuid is null or s.unidade_solicitante_id = {4}::uuid)
              and ({5}::text is null or s.procedimento_texto = {5}::text)
              and ({6}::int is null or s.prioridade = {6}::int)
              and ({7}::boolean or s.unidade_executante_id = any({8}::uuid[]))
        )
        """;

    public async Task<DemandaResumoDto> ResumoAsync(DemandaFiltro f, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        var sql = Base + """
            select
              (select count(*) from base)::int                                                  "Regulados",
              (select count(espera) from base)::int                                             "ComEspera",
              coalesce((select percentile_disc(0.5) within group (order by espera)
                        from base where espera is not null), 0)::int                            "EsperaMediana",
              coalesce((select percentile_disc(0.9) within group (order by espera)
                        from base where espera is not null), 0)::int                            "EsperaP90",
              coalesce((select max(espera) from base), 0)::int                                  "EsperaMaxima",
              coalesce((select percentile_disc(0.5) within group (order by espera_reg)
                        from base where espera_reg is not null), 0)::int                        "EsperaRegulacaoMediana",
              (select count(*) from base where espera <= 30)::int                               "Ate30Dias",
              (select count(*) from base where espera > 180)::int                               "Acima180Dias",
              (select count(*) from base where espera_bruta < 0)::int                           "Inconsistentes",
              (select count(distinct proc) from base)::int                                      "Procedimentos",
              (select count(distinct us) from base where us is not null)::int                   "UnidadesSolicitantes",
              (select count(*) from base where status_confirmacao = 2)::int                     "Confirmados"
            """;

        var linhas = await db.Database
            .SqlQueryRaw<DemandaResumoDto>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);

        return linhas.FirstOrDefault() ?? new DemandaResumoDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<IReadOnlyList<DemandaProcedimentoDto>> ProcedimentosAsync(
        DemandaFiltro f, string ordenarPor, int limite, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        // Nome fechado, nunca o texto do cliente na consulta. Ordenar por espera é a leitura que
        // encontra o gargalo — o procedimento que demora não costuma ser o de maior volume.
        var ordem = ordenarPor switch
        {
            "espera" => "coalesce(percentile_disc(0.5) within group (order by espera), 0) desc, count(*) desc",
            "atraso" => "count(*) filter (where espera > 90) desc, count(*) desc",
            _ => "count(*) desc",
        };

        var sql = Base + $"""
            select proc                                                                    "Procedimento",
                   count(*)::int                                                           "Volume",
                   coalesce(percentile_disc(0.5) within group (order by espera), 0)::int    "EsperaMediana",
                   coalesce(percentile_disc(0.9) within group (order by espera), 0)::int    "EsperaP90",
                   count(*) filter (where espera > 90)::int                                "Acima90Dias",
                   count(distinct us)::int                                                 "UnidadesSolicitantes",
                   count(distinct ux)::int                                                 "UnidadesExecutantes"
            from base
            group by proc
            order by {ordem}
            limit @p9
            """;

        return await db.Database
            .SqlQueryRaw<DemandaProcedimentoDto>(
                Formatar(sql), [.. Parametros(f, veTudo, unidades), Math.Clamp(limite, 1, 100)])
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DemandaFaixaEsperaDto>> FaixasEsperaAsync(
        DemandaFiltro f, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        // LEFT JOIN a partir das faixas: faixa vazia precisa aparecer com zero, senão o histograma
        // muda de forma conforme o filtro e deixa de ser comparável entre duas consultas.
        var sql = Base + $"""
            select f.ordem::int "Ordem", f.rotulo "Rotulo", count(b.id)::int "Volume"
            from {Faixas}
            left join base b on b.espera between f.lo and f.hi
            group by f.ordem, f.rotulo
            order by f.ordem
            """;

        return await db.Database
            .SqlQueryRaw<DemandaFaixaEsperaDto>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DemandaOrigemDto>> OrigemAsync(
        DemandaFiltro f, string eixo, int limite, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);
        var coluna = eixo == "executante" ? "b.ux" : "b.us";
        var vazio = eixo == "executante" ? "(sem executante)" : "(sem solicitante)";

        var sql = Base + $"""
            select coalesce({coluna}::text, '—')                                            "Chave",
                   coalesce(max(u.nome), '{vazio}')                                         "Rotulo",
                   count(*)::int                                                            "Volume",
                   coalesce(percentile_disc(0.5) within group (order by b.espera), 0)::int  "EsperaMediana"
            from base b
            left join smsmarica.unidade u on u.id = {coluna}
            group by 1
            order by 3 desc
            limit @p9
            """;

        return await db.Database
            .SqlQueryRaw<DemandaOrigemDto>(
                Formatar(sql), [.. Parametros(f, veTudo, unidades), Math.Clamp(limite, 1, 100)])
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DemandaSerieDto>> SerieAsync(
        DemandaFiltro f, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        // Agrupa pelo MESMO eixo de data que filtrou. Misturar os dois (filtrar por agendada e
        // agrupar por solicitada) produziria meses com volume que o filtro não deixou entrar.
        var sql = Base + """
            select date_trunc('month', case when @p2::text = 'solicitada' then dsol else dag end)::date "Mes",
                   count(*)::int                                                            "Volume",
                   coalesce(percentile_disc(0.5) within group (order by espera), 0)::int    "EsperaMediana",
                   coalesce(percentile_disc(0.9) within group (order by espera), 0)::int    "EsperaP90"
            from base
            group by 1
            order by 1
            """;

        return await db.Database
            .SqlQueryRaw<DemandaSerieDto>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);
    }

    public async Task<DemandaOpcoesDto> OpcoesAsync(CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        // Só o que aparece no último ano: a lista de procedimentos do histórico inteiro passa de
        // mil entradas e transforma o seletor num obstáculo em vez de um atalho.
        var corte = DateOnly.FromDateTime(
            Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow)).AddYears(-1);
        var corteUtc = Common.Tempo.FusoBrasilia.DeBrasiliaParaUtc(corte.ToDateTime(TimeOnly.MinValue));

        var q = db.Solicitacoes.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CanceladoEm == null && s.DataAgendada >= corteUtc);

        if (!veTudo) q = q.Where(s => unidades.Contains(s.UnidadeExecutanteId));

        var executantes = await q
            .Select(s => new { Id = s.UnidadeExecutanteId, Nome = s.UnidadeExecutante!.Nome })
            .Distinct().OrderBy(x => x.Nome)
            .Select(x => new OpcaoDto(x.Id.ToString(), x.Nome))
            .ToListAsync(ct);

        var solicitantes = await q.Where(s => s.UnidadeSolicitanteId != null)
            .Select(s => new { Id = s.UnidadeSolicitanteId!.Value, Nome = s.UnidadeSolicitante!.Nome })
            .Distinct().OrderBy(x => x.Nome)
            .Select(x => new OpcaoDto(x.Id.ToString(), x.Nome))
            .ToListAsync(ct);

        var procedimentos = await q.Where(s => s.ProcedimentoTexto != null && s.ProcedimentoTexto != "")
            .Select(s => s.ProcedimentoTexto!)
            .Distinct().OrderBy(x => x)
            .Select(x => new OpcaoDto(x, x))
            .ToListAsync(ct);

        return new DemandaOpcoesDto(executantes, solicitantes, procedimentos);
    }

    public async Task<AgendaCoberturaDto> CoberturaAsync(CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);
        var hoje = DateOnly.FromDateTime(Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow));

        var q = db.Solicitacoes.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.CanceladoEm == null && s.DataAgendada != null);

        if (!veTudo) q = q.Where(s => unidades.Contains(s.UnidadeExecutanteId));

        var faixa = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Menor = g.Min(s => s.DataAgendada),
                Maior = g.Max(s => s.DataAgendada),
                Total = g.Count(),
            })
            .FirstOrDefaultAsync(ct);

        var escalas = db.SisregEscalas.AsNoTracking()
            .Where(e => e.Status == Data.Entities.Enums.StatusEscalaSisreg.Ativa
                && !e.Ausente && e.VigenciaFim >= hoje);

        if (!veTudo) escalas = escalas.Where(e => unidades.Contains(e.UnidadeId));

        var ultimaEscala = await escalas
            .OrderByDescending(e => e.VigenciaFim)
            .Select(e => (DateOnly?)e.VigenciaFim)
            .FirstOrDefaultAsync(ct);

        static DateOnly? Dia(DateTime? utc) => utc is null
            ? null
            : DateOnly.FromDateTime(Common.Tempo.FusoBrasilia.ParaExibicao(utc.Value));

        return new AgendaCoberturaDto(
            Dia(faixa?.Menor), Dia(faixa?.Maior), ultimaEscala, faixa?.Total ?? 0);
    }

    // ------------------------------------------------------------------ apoio

    private async Task<(bool VeTudo, Guid[] Unidades)> EscopoAsync(CancellationToken ct)
    {
        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        return (escopo.VeTudo, [.. escopo.Unidades]);
    }

    /// <summary>Troca os marcadores <c>{n}</c> pelos <c>@pN</c> do Npgsql.</summary>
    private static string Formatar(string sql)
    {
        for (var i = 0; i < 10; i++) sql = sql.Replace($"{{{i}}}", $"@p{i}");
        return sql;
    }

    private static object[] Parametros(DemandaFiltro f, bool veTudo, Guid[] unidades) =>
    [
        // DateOnly direto, NUNCA `ToDateTime`: o Npgsql infere `timestamptz` para um DateTime e
        // recusa `Kind=Unspecified` antes mesmo de chegar ao `::date` do SQL — a consulta morre com
        // "Cannot write DateTime with Kind=Unspecified". Como DateOnly, o parâmetro vai como `date`.
        f.De,
        f.Ate,
        // Fechado num par de valores: o parâmetro é ligado, mas manter o domínio explícito evita
        // que um valor novo passe adiante e mude o eixo em silêncio.
        f.EixoData == "solicitada" ? "solicitada" : "agendada",
        (object?)f.UnidadeExecutanteId ?? DBNull.Value,
        (object?)f.UnidadeSolicitanteId ?? DBNull.Value,
        (object?)f.Procedimento ?? DBNull.Value,
        (object?)f.Prioridade ?? DBNull.Value,
        veTudo,
        unidades,
    ];
}
