using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Data;

// `AgendaRegulacao` e nao `Agenda`: isto NAO e uma agenda propria do municipio — e a leitura da
// agenda REGULADA pelo SISREG, que nos apenas observamos. O nome generico fica deliberadamente
// livre para o dia em que existir uma agenda de verdade, com marcacao nossa.
namespace SMSMais.Core.AgendaRegulacao;

public interface IAgendaAnaliseService
{
    Task<AgendaResumoDto> ResumoAsync(AgendaFiltro filtro, CancellationToken ct = default);

    Task<PaginaAgendaDto> ListarDiasAsync(
        AgendaFiltro filtro, int pagina, int tamanho, CancellationToken ct = default);

    Task<AgendaDiaDetalheDto> DetalharDiaAsync(
        Guid unidadeId, string profissionalCpf, DateOnly data, CancellationToken ct = default);

    Task<IReadOnlyList<AgendaRankingItemDto>> RankingAsync(
        AgendaFiltro filtro, string eixo, int limite, CancellationToken ct = default);

    Task<IReadOnlyList<AgendaPorDiaSemanaDto>> PorDiaSemanaAsync(
        AgendaFiltro filtro, CancellationToken ct = default);

    Task<IReadOnlyList<AgendaSerieDiaDto>> SerieAsync(
        AgendaFiltro filtro, CancellationToken ct = default);

    Task<AgendaOpcoesDto> OpcoesAsync(CancellationToken ct = default);
}

/// <summary>
/// A Agenda: cruza a <b>oferta</b> publicada pelo SISREG (<c>sisreg_escala</c>) com a <b>ocupação</b>
/// que já importamos (<c>solicitacao</c>).
///
/// <para><b>O grão é (unidade × profissional × dia).</b> Não entra procedimento na chave do
/// cruzamento, e isso é decisão, não simplificação: a escala do SISREG é publicada em <b>grupos</b>
/// (código terminado em <c>000</c>) que se expandem em itens no agendamento, então casar por
/// procedimento perde metade dos casos — medido em 05/09/2026: 7.172 agendamentos ficariam sem
/// oferta que os explicasse. Por profissional e dia, o casamento sobe para 69%, e é também o
/// recorte que a operação usa ("a agenda do Dr. Fulano na sexta"). O procedimento aparece como
/// detalhe dentro do dia, onde é informação, não chave.</para>
///
/// <para><b>Tudo em dia de Brasília.</b> Nenhuma consulta usa <c>current_date</c>: ele já virou o
/// dia seguinte às 21h e faria a mesma tela mostrar números diferentes conforme a hora — medido em
/// 05/09/2026, dava 912 contra 934 escalas vigentes e 60 vagas de diferença.</para>
///
/// <para><b>Por que SQL cru.</b> Expandir uma escala semanal em ocorrências é um
/// <c>generate_series</c> cruzado com o dia da semana; fazer isso em memória exigiria carregar as
/// escalas todas e reimplementar em C# o que o banco resolve num passo.</para>
/// </summary>
public sealed class AgendaAnaliseService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IAgendaAnaliseService
{
    /// <summary>
    /// O CTE que produz a oferta e a ocupação já recortadas. Todo endpoint parte daqui — uma
    /// definição só de "o que é oferta" e "o que é ocupação" evita que duas telas discordem sobre
    /// o mesmo número, que é o defeito clássico de painel montado consulta a consulta.
    /// </summary>
    private const string Base = """
        with dias as (
            select generate_series({0}::date, {1}::date, '1 day')::date d
        ),
        oferta as (
            select e.unidade_id, e.profissional_cpf, d.d,
                   max(e.profissional_nome) profissional_nome,
                   max(e.cbo_descricao) cbo,
                   sum(e.vagas_total) vagas,
                   sum(e.vagas_primeira_vez) v1,
                   sum(e.vagas_retorno) vr,
                   sum(e.vagas_reserva) vres,
                   min(e.hora_inicio) hora_inicio,
                   max(e.hora_fim) hora_fim,
                   count(*) blocos,
                   string_agg(distinct e.procedimento_nome, ', ' order by e.procedimento_nome) procedimentos
            from smsmarica.sisreg_escala e
            join dias d on extract(dow from d.d) = e.dia_semana
                       and d.d between e.vigencia_inicio and e.vigencia_fim
            where e.status = 1 and not e.ausente
              and ({2}::uuid is null or e.unidade_id = {2}::uuid)
              and ({3}::text is null or e.cbo_codigo = {3}::text)
              and ({4}::text is null or e.profissional_cpf = {4}::text)
              and ({5}::text is null or e.procedimento_codigo = {5}::text)
              and ({6}::boolean or e.unidade_id = any({7}::uuid[]))
            group by 1, 2, 3
        ),
        ocupacao as (
            select s.unidade_executante_id unidade_id,
                   s.profissional_executante_cpf profissional_cpf,
                   (s.data_agendada at time zone 'America/Sao_Paulo')::date d,
                   count(*) agendados
            from smsmarica.solicitacao s
            where s.excluido_em is null and s.cancelado_em is null
              and s.data_agendada is not null
              and s.profissional_executante_cpf is not null
              and (s.data_agendada at time zone 'America/Sao_Paulo')::date between {0}::date and {1}::date
              and ({2}::uuid is null or s.unidade_executante_id = {2}::uuid)
              and ({4}::text is null or s.profissional_executante_cpf = {4}::text)
              and ({6}::boolean or s.unidade_executante_id = any({7}::uuid[]))
            group by 1, 2, 3
        )
        """;

    public async Task<AgendaResumoDto> ResumoAsync(AgendaFiltro f, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        var sql = Base + """
            select
              coalesce((select sum(vagas) from oferta), 0)::int                                   "Vagas",
              coalesce((select sum(agendados) from ocupacao), 0)::int                              "Agendados",
              case when coalesce((select sum(vagas) from oferta), 0) = 0 then 0
                   else (coalesce((select sum(agendados) from ocupacao), 0) * 100
                         / (select sum(vagas) from oferta)) end::int                               "OcupacaoPercentual",
              (select count(*) from oferta where vagas > 0)::int                                   "DiasComOferta",
              (select count(*) from oferta o where o.vagas > 0 and not exists (
                   select 1 from ocupacao c where c.unidade_id = o.unidade_id
                     and c.profissional_cpf = o.profissional_cpf and c.d = o.d))::int              "DiasOciosos",
              (select count(*) from ocupacao c join oferta o on o.unidade_id = c.unidade_id
                     and o.profissional_cpf = c.profissional_cpf and o.d = c.d
                   where c.agendados > o.vagas)::int                                               "DiasSobrecarregados",
              coalesce((select sum(c.agendados) from ocupacao c where not exists (
                   select 1 from oferta o where o.unidade_id = c.unidade_id
                     and o.profissional_cpf = c.profissional_cpf and o.d = c.d)), 0)::int          "AgendadosSemOferta",
              coalesce((select sum(v1) from oferta), 0)::int                                       "VagasPrimeiraVez",
              coalesce((select sum(vr) from oferta), 0)::int                                       "VagasRetorno",
              coalesce((select sum(vres) from oferta), 0)::int                                     "VagasReserva",
              (select count(distinct profissional_cpf) from oferta)::int                           "Profissionais",
              (select count(distinct unidade_id) from oferta)::int                                 "Unidades"
            """;

        var linhas = await db.Database
            .SqlQueryRaw<AgendaResumoDto>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);

        return linhas.FirstOrDefault()
               ?? new AgendaResumoDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<PaginaAgendaDto> ListarDiasAsync(
        AgendaFiltro f, int pagina, int tamanho, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);
        var take = Math.Clamp(tamanho, 1, 500);
        var skip = Math.Max(0, pagina) * take;

        // LEFT JOIN pela oferta e UNION com a ocupação órfã: dia com agendamento e sem escala
        // precisa aparecer, senão a tela esconderia justamente o caso que merece investigação.
        var sql = Base + """
            , juntas as (
                select o.d, o.unidade_id, o.profissional_cpf, o.profissional_nome, o.cbo,
                       o.vagas, coalesce(c.agendados, 0) agendados,
                       o.hora_inicio, o.hora_fim, o.blocos, o.procedimentos
                from oferta o
                left join ocupacao c on c.unidade_id = o.unidade_id
                     and c.profissional_cpf = o.profissional_cpf and c.d = o.d
                union all
                select c.d, c.unidade_id, c.profissional_cpf,
                       coalesce((select max(pu.nome) from smsmarica.sisreg_profissional_unidade pu
                                 where pu.cpf = c.profissional_cpf), '(sem escala)'),
                       null, 0, c.agendados, null, null, 0, ''
                from ocupacao c
                where not exists (select 1 from oferta o where o.unidade_id = c.unidade_id
                                  and o.profissional_cpf = c.profissional_cpf and o.d = c.d)
            )
            select j.d "Data", j.unidade_id "UnidadeId", u.nome "UnidadeNome",
                   j.profissional_cpf "ProfissionalCpf", j.profissional_nome "ProfissionalNome",
                   j.cbo "Cbo", j.vagas::int "Vagas", j.agendados::int "Agendados",
                   greatest(j.vagas - j.agendados, 0)::int "Livres",
                   j.hora_inicio "HoraInicio", j.hora_fim "HoraFim", j.blocos::int "Blocos",
                   coalesce(j.procedimentos, '') "Procedimentos"
            from juntas j
            join smsmarica.unidade u on u.id = j.unidade_id
            order by j.d, u.nome, j.profissional_nome
            limit {8} offset {9}
            """;

        var itens = await db.Database
            .SqlQueryRaw<AgendaDiaDto>(Formatar(sql), [.. Parametros(f, veTudo, unidades), take, skip])
            .ToListAsync(ct);

        var total = await ContarAsync(f, veTudo, unidades, ct);
        return new PaginaAgendaDto(total, itens);
    }

    private async Task<int> ContarAsync(
        AgendaFiltro f, bool veTudo, Guid[] unidades, CancellationToken ct)
    {
        var sql = Base + """
            select ((select count(*) from oferta)
                  + (select count(*) from ocupacao c where not exists (
                        select 1 from oferta o where o.unidade_id = c.unidade_id
                          and o.profissional_cpf = c.profissional_cpf and o.d = c.d)))::int "Value"
            """;

        var linhas = await db.Database
            .SqlQueryRaw<int>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);

        return linhas.FirstOrDefault();
    }

    public async Task<IReadOnlyList<AgendaRankingItemDto>> RankingAsync(
        AgendaFiltro f, string eixo, int limite, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        // Só três eixos, escolhidos por nome fechado — o valor nunca entra na consulta como texto.
        var (chave, rotulo, join) = eixo switch
        {
            "profissional" => ("o.profissional_cpf", "max(o.profissional_nome)", ""),
            "especialidade" => ("coalesce(o.cbo, '(sem CBO)')", "coalesce(o.cbo, '(sem CBO)')", ""),
            _ => ("o.unidade_id::text", "max(u.nome)",
                  "join smsmarica.unidade u on u.id = o.unidade_id"),
        };

        var sql = Base + $$"""
            select {{chave}} "Chave", {{rotulo}} "Rotulo",
                   sum(o.vagas)::int "Vagas",
                   coalesce(sum(c.agendados), 0)::int "Agendados",
                   greatest(sum(o.vagas) - coalesce(sum(c.agendados), 0), 0)::int "Livres",
                   case when sum(o.vagas) = 0 then 0
                        else (coalesce(sum(c.agendados), 0) * 100 / sum(o.vagas)) end::int "OcupacaoPercentual"
            from oferta o
            {{join}}
            left join ocupacao c on c.unidade_id = o.unidade_id
                 and c.profissional_cpf = o.profissional_cpf and c.d = o.d
            group by 1
            order by sum(o.vagas) desc
            limit @p8
            """;

        return await db.Database
            .SqlQueryRaw<AgendaRankingItemDto>(
                Formatar(sql), [.. Parametros(f, veTudo, unidades), Math.Clamp(limite, 1, 100)])
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgendaPorDiaSemanaDto>> PorDiaSemanaAsync(
        AgendaFiltro f, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        var sql = Base + """
            select extract(dow from o.d)::int "DiaSemana",
                   case extract(dow from o.d)
                     when 0 then 'Domingo' when 1 then 'Segunda' when 2 then 'Terça'
                     when 3 then 'Quarta' when 4 then 'Quinta' when 5 then 'Sexta'
                     else 'Sábado' end "Rotulo",
                   sum(o.vagas)::int "Vagas",
                   coalesce(sum(c.agendados), 0)::int "Agendados",
                   case when sum(o.vagas) = 0 then 0
                        else (coalesce(sum(c.agendados), 0) * 100 / sum(o.vagas)) end::int "OcupacaoPercentual"
            from oferta o
            left join ocupacao c on c.unidade_id = o.unidade_id
                 and c.profissional_cpf = o.profissional_cpf and c.d = o.d
            group by 1, 2
            order by 1
            """;

        return await db.Database
            .SqlQueryRaw<AgendaPorDiaSemanaDto>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgendaSerieDiaDto>> SerieAsync(
        AgendaFiltro f, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);

        // Parte de `dias`, não de `oferta`: dia sem escala nenhuma tem de aparecer com zero. Se a
        // linha do gráfico simplesmente pulasse o feriado, o traço ligaria véspera e dia seguinte
        // e a queda desapareceria da tela — que é justamente o que se quer enxergar.
        var sql = Base + """
            select d.d "Data",
                   coalesce((select sum(o.vagas) from oferta o where o.d = d.d), 0)::int "Vagas",
                   coalesce((select sum(c.agendados) from ocupacao c where c.d = d.d), 0)::int "Agendados"
            from dias d
            order by d.d
            """;

        return await db.Database
            .SqlQueryRaw<AgendaSerieDiaDto>(Formatar(sql), Parametros(f, veTudo, unidades))
            .ToListAsync(ct);
    }

    public async Task<AgendaDiaDetalheDto> DetalharDiaAsync(
        Guid unidadeId, string profissionalCpf, DateOnly data, CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);
        if (!veTudo && !unidades.Contains(unidadeId))
        {
            throw new Common.Excecoes.NaoEncontradoException("Agenda do dia", $"{unidadeId}/{data:yyyy-MM-dd}");
        }

        var diaSemana = (int)data.DayOfWeek;

        var blocos = await db.SisregEscalas.AsNoTracking()
            .Where(e => e.UnidadeId == unidadeId
                && e.ProfissionalCpf == profissionalCpf
                && e.Status == Data.Entities.Enums.StatusEscalaSisreg.Ativa
                && !e.Ausente
                && (int)e.DiaSemana == diaSemana
                && e.VigenciaInicio <= data && e.VigenciaFim >= data)
            .OrderBy(e => e.HoraInicio)
            .Select(e => new
            {
                e.HoraInicio, e.HoraFim, e.ProcedimentoCodigo, e.ProcedimentoNome,
                e.VagasPrimeiraVez, e.VagasRetorno, e.VagasReserva, e.VagasTotal,
                e.ProfissionalNome, e.CboDescricao,
            })
            .ToListAsync(ct);

        var inicioUtc = Common.Tempo.FusoBrasilia.DeBrasiliaParaUtc(data.ToDateTime(TimeOnly.MinValue));
        var fimUtc = Common.Tempo.FusoBrasilia.DeBrasiliaParaUtc(data.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var ocupantes = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.UnidadeExecutanteId == unidadeId
                && s.ProfissionalExecutanteCpf == profissionalCpf
                && s.ExcluidoEm == null && s.CanceladoEm == null
                && s.DataAgendada >= inicioUtc && s.DataAgendada < fimUtc)
            .OrderBy(s => s.DataAgendada)
            .Select(s => new OcupanteDto(
                s.Id,
                s.CodigoSolicitacao,
                s.DataAgendada!.Value,
                null,
                s.ProcedimentoTexto,
                db.Unidades.Where(u => u.Id == s.UnidadeSolicitanteId).Select(u => u.Nome).FirstOrDefault(),
                (int)s.StatusConfirmacao))
            .ToListAsync(ct);

        var unidadeNome = await db.Unidades.AsNoTracking()
            .Where(u => u.Id == unidadeId).Select(u => u.Nome).FirstOrDefaultAsync(ct) ?? "—";

        return new AgendaDiaDetalheDto(
            data,
            unidadeNome,
            blocos.FirstOrDefault()?.ProfissionalNome ?? "—",
            blocos.FirstOrDefault()?.CboDescricao,
            blocos.Sum(b => b.VagasTotal),
            ocupantes.Count,
            [.. blocos.Select(b => new BlocoEscalaDto(
                b.HoraInicio, b.HoraFim, b.ProcedimentoCodigo, b.ProcedimentoNome,
                b.VagasPrimeiraVez, b.VagasRetorno, b.VagasReserva,
                // Duração ÷ vagas. É dedução: os minutos do SISREG vêm zerados em boa parte das
                // linhas, então multiplicar por eles daria zero justamente onde mais se quer saber.
                b.VagasTotal > 0
                    ? (int)((b.HoraFim.ToTimeSpan() - b.HoraInicio.ToTimeSpan()).TotalMinutes / b.VagasTotal)
                    : null))],
            ocupantes);
    }

    public async Task<AgendaOpcoesDto> OpcoesAsync(CancellationToken ct = default)
    {
        var (veTudo, unidades) = await EscopoAsync(ct);
        var hoje = DateOnly.FromDateTime(Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow));

        // Só o que ainda tem escala VIGENTE: oferecer filtro para quem saiu da grade só produz
        // consulta vazia e a impressão de que o sistema perdeu o dado.
        var q = db.SisregEscalas.AsNoTracking()
            .Where(e => e.Status == Data.Entities.Enums.StatusEscalaSisreg.Ativa
                && !e.Ausente && e.VigenciaFim >= hoje);

        if (!veTudo) q = q.Where(e => unidades.Contains(e.UnidadeId));

        var unidadesOpc = await q
            .Select(e => new { e.UnidadeId, e.UnidadeNomeSisreg })
            .Distinct().OrderBy(x => x.UnidadeNomeSisreg)
            .Select(x => new OpcaoDto(x.UnidadeId.ToString(), x.UnidadeNomeSisreg))
            .ToListAsync(ct);

        var cbos = await q.Where(e => e.CboCodigo != null)
            .Select(e => new { e.CboCodigo, e.CboDescricao })
            .Distinct().OrderBy(x => x.CboDescricao)
            .Select(x => new OpcaoDto(x.CboCodigo!, x.CboDescricao ?? x.CboCodigo!))
            .ToListAsync(ct);

        var profissionais = await q
            .Select(e => new { e.ProfissionalCpf, e.ProfissionalNome })
            .Distinct().OrderBy(x => x.ProfissionalNome)
            .Select(x => new OpcaoDto(x.ProfissionalCpf, x.ProfissionalNome))
            .ToListAsync(ct);

        var procedimentos = await q
            .Select(e => new { e.ProcedimentoCodigo, e.ProcedimentoNome })
            .Distinct().OrderBy(x => x.ProcedimentoNome)
            .Select(x => new OpcaoDto(x.ProcedimentoCodigo, x.ProcedimentoNome))
            .ToListAsync(ct);

        return new AgendaOpcoesDto(unidadesOpc, cbos, profissionais, procedimentos);
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

    private static object[] Parametros(AgendaFiltro f, bool veTudo, Guid[] unidades) =>
    [
        // DateOnly direto, NUNCA `ToDateTime`: o Npgsql infere `timestamptz` para um DateTime e
        // recusa `Kind=Unspecified` antes mesmo de chegar ao `::date` do SQL — a consulta morre com
        // "Cannot write DateTime with Kind=Unspecified". Como DateOnly, o parâmetro vai como `date`.
        f.De,
        f.Ate,
        (object?)f.UnidadeId ?? DBNull.Value,
        (object?)f.Cbo ?? DBNull.Value,
        (object?)f.ProfissionalCpf ?? DBNull.Value,
        (object?)f.ProcedimentoCodigo ?? DBNull.Value,
        veTudo,
        unidades,
    ];
}
