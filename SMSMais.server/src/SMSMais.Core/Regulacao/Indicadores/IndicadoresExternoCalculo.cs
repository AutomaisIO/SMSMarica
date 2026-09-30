using System.Globalization;
using System.Text.RegularExpressions;
using SMSMais.Core.Regulacao.Indicadores.Dtos;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>
/// Indicadores do SER (SES-RJ) e do SERNIT (Niterói) — mesmo modelo de espelho
/// (<c>{p}_solicitacao</c> + <c>{p}_evento</c> com <c>TipoEventoExterno</c>). Definições do relatório de
/// 30/09/2026: agendamento = 1º Agendar; absenteísmo = registro de chegada da executora; fila de fim de mês
/// pelo último estado da trilha até a data; motivo = texto do cancelamento completado pelo último FollowUP.
/// </summary>
internal static partial class IndicadoresExternoCalculo
{
    private const string Fuso = "America/Sao_Paulo";

    public static async Task<IndicadoresRegulacaoDto> CalcularAsync(
        Sql sql, PeriodoIndicadores p, FonteIndicadorRegulacao fonte, DateTime agora, CancellationToken ct)
    {
        var (pre, sigla, nome) = fonte == FonteIndicadorRegulacao.Ser
            ? ("ser", "SER", "SER — Sistema Estadual de Regulação (SES-RJ)")
            : ("sernit", "SERNIT", "SER Niterói — Sistema de Regulação de Niterói");
        var s = $"smsmarica.{pre}_solicitacao";
        var e = $"smsmarica.{pre}_evento";
        var fk = $"{pre}_solicitacao_id";
        var f = new Series(p);
        var secoes = new List<SecaoIndicadorDto>();
        var tipos = new Dictionary<int, string> { [1] = "Consultas", [2] = "Exames" };

        // agendamentos (1º Agendar) e solicitações, por tipo
        var ag = await PorTipoAsync(sql, $"""
            with pa as (select ev.{fk} sid, min(ev.data_evento) d from {e} ev where ev.tipo_evento = 5 group by 1)
            select to_char((pa.d at time zone '{Fuso}')::date,'YYYY-MM'), so.tipo, count(*)
            from pa join {s} so on so.id = pa.sid and so.excluido_em is null
            where (pa.d at time zone '{Fuso}')::date between @p0 and @p1 group by 1, 2
            """, tipos, p, ct);
        var agTot = Somar(ag, p);
        var so = await PorTipoAsync(sql, $"""
            select to_char(data_solicitacao,'YYYY-MM'), tipo, count(*) from {s}
            where excluido_em is null and data_solicitacao between @p0 and @p1 group by 1, 2
            """, tipos, p, ct);
        var soTot = Somar(so, p);

        secoes.Add(new SecaoIndicadorDto("vagas", "Vagas disponibilizadas e utilizadas",
            $"A oferta de vagas do {sigla} pertence ao gestor do sistema e não é exibida ao município solicitante. Utilização = solicitações do município que receberam agendamento no mês.",
            false,
            [
                f.Serie("Vagas ofertadas", new Dictionary<string, int>(), SeloIndicador.Indisponivel, $"O {sigla} não expõe a oferta ao município solicitante."),
                f.Serie("Vagas utilizadas (agendamentos no mês)", agTot, SeloIndicador.Oficial, "Primeiro agendamento de cada solicitação (remarcações não somam).", destaque: true),
            ],
            new GraficoIndicadorDto("barras", ["Vagas utilizadas (agendamentos no mês)"]), null, [], null));

        // absenteísmo: último registro de chegada de cada solicitação
        var cheg = await sql.LinhasAsync($"""
            with uc as (select distinct on (ev.{fk}) ev.{fk} sid, ev.data_evento d, ev.estado_atual est
                        from {e} ev where ev.tipo_evento = 6 order by ev.{fk}, ev.data_evento desc)
            select to_char((uc.d at time zone '{Fuso}')::date,'YYYY-MM'),
                   count(*) filter (where uc.est ilike '%n_o confirmada%'),
                   count(*) filter (where uc.est not ilike '%n_o confirmada%')
            from uc join {s} so on so.id = uc.sid and so.excluido_em is null
            where (uc.d at time zone '{Fuso}')::date between @p0 and @p1 group by 1
            """, ct, p.Inicio, p.Fim);
        var nconf = cheg.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[1], CultureInfo.InvariantCulture));
        var conf = cheg.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[2], CultureInfo.InvariantCulture));
        var totCheg = p.Meses.Where(m => nconf.ContainsKey(m) || conf.ContainsKey(m))
            .ToDictionary(m => m, m => nconf.GetValueOrDefault(m) + conf.GetValueOrDefault(m));
        var semRegistro = await sql.PorMesAsync($"""
            select to_char(to_date(substring(agendado_para_texto from '^([0-9][0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9])'),'DD/MM/YYYY'),'YYYY-MM'), count(*)
            from {s} where excluido_em is null and situacao = 3 and agendado_para_texto ~ '^[0-9][0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9]'
              and to_date(substring(agendado_para_texto from '^([0-9][0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9])'),'DD/MM/YYYY')
                  between @p0 and least(@p1, current_date - 1)
            group by 1
            """, ct, p.Inicio, p.Fim);
        secoes.Add(new SecaoIndicadorDto("absenteismo", "Absenteísmo",
            $"Registro de chegada feito pela unidade executora no {sigla} ('Chegada no destino').",
            false,
            [
                f.Serie("Chegadas registradas no mês", totCheg, SeloIndicador.Oficial),
                f.Serie("chegada confirmada (compareceu)", conf, SeloIndicador.Oficial, subitem: true),
                f.Serie("chegada não confirmada (faltou)", nconf, SeloIndicador.Oficial, destaque: true, subitem: true),
                f.Serie("Absenteísmo", f.Pct(nconf, totCheg), SeloIndicador.Calculado, "Chegadas não confirmadas ÷ chegadas registradas no mês.",
                    FormatoIndicador.Percentual, destaque: true, anual: f.PctAnual(p.Meses.ToDictionary(m => m, m => nconf.GetValueOrDefault(m)), totCheg)),
                f.Serie("Agendados sem registro de chegada", semRegistro, SeloIndicador.Calculado,
                    "Solicitações ainda 'Agendada' cuja data já passou — a unidade executora não registrou a chegada."),
            ],
            new GraficoIndicadorDto("barras", ["chegada não confirmada (faltou)"]), null, [], null));

        secoes.Add(new SecaoIndicadorDto("regulados", "Quantitativo regulado de consultas e exames",
            "Solicitações do município que receberam agendamento no mês, por tipo.", false,
            [f.Serie("Total regulado", agTot, SeloIndicador.Oficial, destaque: true),
             .. new[] { "Consultas", "Exames" }.Where(ag.ContainsKey).Select(t => f.Serie(t, ag[t], SeloIndicador.Oficial, subitem: true))],
            new GraficoIndicadorDto("empilhado", new[] { "Consultas", "Exames" }.Where(ag.ContainsKey).ToList()), null, [], null));

        // fila no fim do mês: último estado da trilha até a data (FollowUP/WhatsApp não mudam estado)
        var fila = await sql.LinhasAsync($"""
            with ev as (
                select ev.{fk} sid, (ev.data_evento at time zone '{Fuso}')::date d, ev.estado_atual est,
                       lead((ev.data_evento at time zone '{Fuso}')::date) over (partition by ev.{fk} order by ev.data_evento) prox
                from {e} ev join {s} so on so.id = ev.{fk} and so.excluido_em is null
                where ev.estado_atual is not null and ev.tipo_evento not in (2, 9)),
            pri as (select sid, min(d) d0 from ev group by sid),
            meses as (select (date_trunc('month', g) + interval '1 month - 1 day')::date fim
                      from generate_series(@p0::date, @p1::date, interval '1 month') g)
            select to_char(m.fim,'YYYY-MM'),
                   (select count(distinct ev.sid) from ev where ev.d <= m.fim and (ev.prox is null or ev.prox > m.fim)
                                                           and ev.est in ('Em fila', 'Pendente'))
                 + (select count(*) from {s} so join pri on pri.sid = so.id
                    where so.excluido_em is null and so.data_solicitacao <= m.fim and pri.d0 > m.fim)
                 + (select count(*) from {s} so where so.excluido_em is null and so.data_solicitacao <= m.fim
                      and so.situacao in (1, 2) and not exists (select 1 from pri where pri.sid = so.id)),
                   (select count(*) from {s} so where so.excluido_em is null and so.data_solicitacao <= m.fim
                      and so.situacao in (1, 2) and not exists (select 1 from pri where pri.sid = so.id))
            from meses m
            """, ct, p.Inicio, p.Fim);
        var filaFim = fila.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[1], CultureInfo.InvariantCulture));
        var semTrilha = fila.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[2], CultureInfo.InvariantCulture));
        secoes.Add(new SecaoIndicadorDto("fila", "Fila e solicitações",
            "Solicitações do município aguardando no último dia do mês (situação 'Em fila' ou 'Pendente' naquela data) e solicitações registradas no mês.",
            false,
            [
                f.Serie("Solicitações em fila no fim do mês", filaFim, SeloIndicador.Calculado,
                    "Reconstruída pela trilha de eventos de cada solicitação (último estado até a data).", destaque: true, agregacao: AgregacaoIndicador.UltimoMes),
                f.Serie("das quais sem trilha de eventos", semTrilha, SeloIndicador.Calculado,
                    "Sem histórico legível: contadas pela situação atual (em fila/pendente).", subitem: true, agregacao: AgregacaoIndicador.UltimoMes),
                f.Serie("Solicitações registradas no mês", soTot, SeloIndicador.Oficial),
                .. new[] { "Consultas", "Exames" }.Where(so.ContainsKey).Select(t => f.Serie(t, so[t], SeloIndicador.Oficial, subitem: true)),
            ],
            new GraficoIndicadorDto("barras", ["Solicitações em fila no fim do mês"]), null, [], null));

        // desfechos + motivos (cancelamento completado pelo último FollowUP)
        var cancs = await sql.LinhasAsync($"""
            with uc as (select distinct on (ev.{fk}) ev.{fk} sid, ev.data_evento d, coalesce(ev.observacao,'') obs
                        from {e} ev where ev.tipo_evento = 4 and ev.estado_atual ilike 'cancelad%'
                        order by ev.{fk}, ev.data_evento)
            select to_char((uc.d at time zone '{Fuso}')::date,'YYYY-MM'), extract(year from (uc.d at time zone '{Fuso}'))::int,
                   uc.obs, fu.followup_categoria, fu.observacao
            from uc join {s} so on so.id = uc.sid and so.excluido_em is null
            left join lateral (select ev.followup_categoria, ev.observacao from {e} ev
                               where ev.{fk} = uc.sid and ev.tipo_evento = 2 and ev.data_evento <= uc.d
                               order by ev.data_evento desc limit 1) fu on true
            where (uc.d at time zone '{Fuso}')::date between @p0 and @p1
            """, ct, p.Inicio, p.Fim);
        var canc = cancs.GroupBy(l => (string)l[0]!).ToDictionary(g => g.Key, g => g.Count());
        var motAno = cancs.GroupBy(l => Convert.ToInt32(l[1], CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => g.GroupBy(l => MotivoCancelamento.CategoriaComFollowUp((string?)l[2], (string?)l[3], (string?)l[4]))
                .ToDictionary(c => c.Key, c => c.Count()));
        var comFu = cancs.Count(l => l[3] is not null);
        var desfeitas = await sql.PorMesAsync($"""
            select to_char((ev.data_evento at time zone '{Fuso}')::date,'YYYY-MM'), count(distinct ev.{fk}) from {e} ev
            where ev.tipo_evento = 4 and ev.estado_atual not ilike 'cancelad%'
              and (ev.data_evento at time zone '{Fuso}')::date between @p0 and @p1 group by 1
            """, ct, p.Inicio, p.Fim);
        var dev = await sql.PorMesAsync($"""
            select to_char((ev.data_evento at time zone '{Fuso}')::date,'YYYY-MM'), count(distinct ev.{fk}) from {e} ev
            where ev.tipo_evento = 8 and (ev.data_evento at time zone '{Fuso}')::date between @p0 and @p1 group by 1
            """, ct, p.Inicio, p.Fim);
        secoes.Add(new SecaoIndicadorDto("desfechos", "Atendidas, canceladas e excluídas",
            $"Desfechos registrados no {sigla} no mês. O {sigla} não tem situação de 'excluída': a saída da fila sem atendimento é o cancelamento (com motivo).",
            false,
            [
                f.Serie("Atendidas (chegada confirmada)", conf, SeloIndicador.Oficial, destaque: true),
                f.Serie("Canceladas", canc, SeloIndicador.Oficial, "Solicitações cujo cancelamento levou à situação 'Cancelada'.", destaque: true),
                f.Serie("Marcações desfeitas (voltaram à fila)", desfeitas, SeloIndicador.Oficial, "Cancelamento do agendamento com retorno à fila — não é exclusão."),
                f.Serie("Devolvidas para a regulação", dev, SeloIndicador.Oficial, "Voltaram à fila para nova regulação (não é exclusão)."),
            ],
            new GraficoIndicadorDto("barras", ["Atendidas (chegada confirmada)", "Canceladas"]),
            cancs.Count == 0 ? null : TabelaMotivos(motAno, $"Motivos dos cancelamentos no {sigla}",
                $"Observação registrada no cancelamento, completada pelo último FollowUP anterior quando o texto do cancelamento é genérico (ex.: 'não respondida no prazo' + FollowUP 'sem contato: diversas tentativas'). {comFu} de {cancs.Count} cancelamentos têm FollowUP antes. Texto livre não reproduzido."),
            [], null));

        secoes.Add(await EsperaAsync(sql, f, p, s, e, fk, ct));
        secoes.Add(await JudicialAsync(sql, f, p, pre, s, e, fk, sigla, tipos, ct));

        var cob = (await sql.LinhasAsync($"""
            select (select count(*) from {s} where excluido_em is null),
                   (select max(finalizado_em) from smsmarica.{pre}_varredura_execucao where status = 3)
            """, ct))[0];
        var cobertura = new List<string>
        {
            $"Espelho das solicitações do município no {sigla} ({Formatar.Numero(cob[0])} solicitações), última varredura completa em {Formatar.Data(cob[1]) ?? "—"}.",
        };
        return new IndicadoresRegulacaoDto(fonte, sigla, nome, p.Meses, agora, cobertura, secoes);
    }

    private static async Task<SecaoIndicadorDto> EsperaAsync(
        Sql sql, Series f, PeriodoIndicadores p, string s, string e, string fk, CancellationToken ct)
    {
        var linhas = await sql.LinhasAsync($"""
            with pa as (select ev.{fk} sid, min(ev.data_evento) d from {e} ev where ev.tipo_evento = 5 group by 1)
            select to_char((pa.d at time zone '{Fuso}')::date,'YYYY-MM'),
                   ((pa.d at time zone '{Fuso}')::date - so.data_solicitacao), so.tipo, coalesce(so.recurso,'(sem recurso)')
            from pa join {s} so on so.id = pa.sid and so.excluido_em is null
            where (pa.d at time zone '{Fuso}')::date between @p0 and @p1 and so.data_solicitacao is not null
              and (pa.d at time zone '{Fuso}')::date >= so.data_solicitacao
            """, ct, p.Inicio, p.Fim);
        var itens = linhas.Select(l => (Mes: (string)l[0]!, Dias: Convert.ToInt32(l[1], CultureInfo.InvariantCulture),
            Tipo: Convert.ToInt32(l[2], CultureInfo.InvariantCulture), Recurso: (string)l[3]!)).ToList();
        var porMes = itens.GroupBy(i => i.Mes).ToDictionary(g => g.Key, g => Tempo.Resumo(g.Select(i => i.Dias)));
        var porAno = itens.GroupBy(i => i.Mes[..4]).ToDictionary(g => g.Key, g => Tempo.Resumo(g.Select(i => i.Dias)));

        Dictionary<string, decimal?> M(Func<ResumoTempoDto, int?> sel) => porMes.ToDictionary(kv => kv.Key, kv => (decimal?)sel(kv.Value));
        Dictionary<string, decimal?> A(Func<ResumoTempoDto, int?> sel) => porAno.ToDictionary(kv => kv.Key, kv => (decimal?)sel(kv.Value));

        var tabelas = new List<TabelaIndicadorDto>();
        foreach (var ano in p.Anos)
            tabelas.Add(Tempo.Top(itens.Where(i => i.Mes.StartsWith(ano, StringComparison.Ordinal)).GroupBy(i => Especialidade(i.Tipo, i.Recurso)), i => i.Dias,
                $"Por especialidade — {ano}{AteMes(p, ano)} (dias até o agendamento)", "Agendados"));
        foreach (var ano in p.Anos)
            tabelas.Add(Tempo.Top(itens.Where(i => i.Mes.StartsWith(ano, StringComparison.Ordinal)).GroupBy(i => i.Recurso), i => i.Dias,
                $"Por procedimento — {ano}{AteMes(p, ano)}, 15 mais agendados", "Agendados"));
        var aguard = await sql.LinhasAsync($"""
            select coalesce(recurso,'(sem recurso)'), count(*), percentile_disc(0.5) within group (order by current_date - data_solicitacao),
                   max(current_date - data_solicitacao)
            from {s} where excluido_em is null and situacao in (1, 2) and data_solicitacao is not null
            group by 1 order by 2 desc limit 15
            """, ct);
        tabelas.Add(new TabelaIndicadorDto("Quem ainda aguarda (em fila ou pendente hoje), 15 procedimentos com mais pacientes",
            ["", "Aguardando", "Mediana (dias)", "Maior (dias)"],
            aguard.Select(l => (IReadOnlyList<string?>)[IndicadoresSisregCalculo.Truncar((string?)l[0]), Formatar.Numero(l[1]), Formatar.Numero(l[2]), Formatar.Numero(l[3])]).ToList(), null));

        return new SecaoIndicadorDto("espera", "Tempo de espera",
            "Dias entre a solicitação e o primeiro agendamento, das solicitações agendadas no mês.", false,
            [
                f.Serie("Espera — mediana (dias)", M(r => r.Mediana), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, destaque: true, anual: A(r => r.Mediana)),
                f.Serie("Espera — 90% até (dias)", M(r => r.P90), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: A(r => r.P90)),
                f.Serie("Espera — menor (dias)", M(r => r.Menor), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: A(r => r.Menor)),
                f.Serie("Espera — maior (dias)", M(r => r.Maior), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: A(r => r.Maior)),
            ],
            new GraficoIndicadorDto("barras", ["Espera — mediana (dias)"]), null, tabelas, null);
    }

    private static async Task<SecaoIndicadorDto> JudicialAsync(
        Sql sql, Series f, PeriodoIndicadores p, string pre, string s, string e, string fk, string sigla,
        IReadOnlyDictionary<int, string> tipos, CancellationToken ct)
    {
        var verificado = (await sql.LinhasAsync($"select max(mandado_judicial_verificado_em) from {s}", ct))[0][0];
        if (verificado is null)
        {
            return new SecaoIndicadorDto("judicial", "Demandas judicializadas",
                $"O {sigla} marca a solicitação com mandado judicial (filtro 'Somente com mandado judicial' da pesquisa). A varredura passa a ler essa marcação — ainda não rodou para este espelho.",
                true, [], null, null, [], null);
        }
        var sitNome = new Dictionary<int, string> { [1] = "Em fila", [2] = "Pendente", [3] = "Agendada", [4] = "Chegada não confirmada", [5] = "Chegada confirmada", [6] = "Cancelada", [7] = "Alta" };
        var jl = await sql.LinhasAsync($"""
            select so.id_{pre}, so.tipo, left(so.recurso, 60), so.data_solicitacao, so.situacao,
              (select min((ev.data_evento at time zone '{Fuso}')::date) from {e} ev where ev.{fk} = so.id and ev.tipo_evento = 5) - so.data_solicitacao,
              (select min((ev.data_evento at time zone '{Fuso}')::date) from {e} ev where ev.{fk} = so.id and ev.tipo_evento = 6) - so.data_solicitacao
            from {s} so where so.excluido_em is null and so.mandado_judicial order by so.data_solicitacao
            """, ct);
        var noPeriodo = jl.Where(l => l[3] is DateOnly d && d >= p.Inicio && d <= p.Fim).ToList();
        var porMes = noPeriodo.GroupBy(l => PeriodoIndicadores.Chave((DateOnly)l[3]!)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var m in p.Meses) porMes.TryAdd(m, 0);
        var tempos = noPeriodo.Where(l => l[5] is not null && Convert.ToInt32(l[5], CultureInfo.InvariantCulture) >= 0)
            .Select(l => Convert.ToInt32(l[5], CultureInfo.InvariantCulture));
        static string Dias(object? v) => v is null || Convert.ToInt32(v, CultureInfo.InvariantCulture) < 0 ? "—" : Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        return new SecaoIndicadorDto("judicial", "Demandas judicializadas",
            $"Solicitações marcadas no {sigla} como 'Mandado judicial'. Total no {sigla}: {jl.Count}; com solicitação no período: {noPeriodo.Count}.",
            false,
            [f.Serie("Solicitações judicializadas (pela data da solicitação)", porMes, SeloIndicador.Oficial, destaque: true)],
            new GraficoIndicadorDto("barras", ["Solicitações judicializadas (pela data da solicitação)"]), null,
            noPeriodo.Count == 0 ? [] :
            [
                new TabelaIndicadorDto("Solicitações judicializadas com solicitação no período",
                    ["ID", "Tipo", "Recurso", "Solicitada em", "Situação atual", "Dias até agendar", "Dias até a chegada"],
                    noPeriodo.Select(l => (IReadOnlyList<string?>)[
                        (string?)l[0], tipos.GetValueOrDefault(Convert.ToInt32(l[1], CultureInfo.InvariantCulture), "—"), (string?)l[2],
                        Formatar.Data(l[3]), sitNome.GetValueOrDefault(Convert.ToInt32(l[4], CultureInfo.InvariantCulture), "—"),
                        Dias(l[5]), Dias(l[6])]).ToList(), null),
            ],
            Tempo.Resumo(tempos));
    }

    internal static TabelaIndicadorDto TabelaMotivos(IReadOnlyDictionary<int, Dictionary<string, int>> porAno, string titulo, string nota)
    {
        var anos = porAno.Keys.OrderBy(a => a).ToList();
        var categorias = porAno.Values.SelectMany(c => c).GroupBy(kv => kv.Key)
            .OrderByDescending(g => g.Sum(kv => kv.Value)).Select(g => g.Key).ToList();
        var colunas = new List<string> { "Motivo" };
        colunas.AddRange(anos.Select(a => a.ToString(CultureInfo.InvariantCulture)));
        colunas.Add("Total");
        var linhas = categorias.Select(cat =>
        {
            var l = new List<string?> { cat };
            l.AddRange(anos.Select(a => Formatar.Numero(porAno[a].GetValueOrDefault(cat))));
            l.Add(Formatar.Numero(anos.Sum(a => porAno[a].GetValueOrDefault(cat))));
            return (IReadOnlyList<string?>)l;
        }).ToList();
        return new TabelaIndicadorDto(titulo, colunas, linhas, nota);
    }

    /// <summary>Especialidade tirada do nome do recurso ("CONSULTA EM X - …", "Ambulatório 1ª vez - X (…)").</summary>
    internal static string Especialidade(int tipo, string recurso)
    {
        var r = string.Join(' ', recurso.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var m1 = RegexConsultaEm().Match(r);
        if (m1.Success) return "Consulta — " + Titulo(m1.Groups[1].Value);
        var m2 = RegexAmbulatorio().Match(r);
        if (m2.Success) return "Consulta — " + Titulo(m2.Groups[1].Value);
        if (tipo == 2) return "Exame — " + Titulo(RegexCorteExame().Split(r, 2)[0]);
        return "Consulta — " + Titulo(r.Split(" - ")[0]);
    }

    private static string AteMes(PeriodoIndicadores p, string ano)
    {
        var ultimo = p.Meses[^1];
        return ultimo.StartsWith(ano, StringComparison.Ordinal) && !ultimo.EndsWith("-12", StringComparison.Ordinal)
            ? $" até {IndicadoresSisregCalculo.MesCurto(ultimo)}" : string.Empty;
    }

    private static string Titulo(string s) => CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(s.Trim().ToLowerInvariant());

    private static async Task<Dictionary<string, Dictionary<string, int>>> PorTipoAsync(
        Sql sql, string consulta, IReadOnlyDictionary<int, string> tipos, PeriodoIndicadores p, CancellationToken ct)
    {
        var r = new Dictionary<string, Dictionary<string, int>>();
        foreach (var l in await sql.LinhasAsync(consulta, ct, p.Inicio, p.Fim))
        {
            var nome = tipos.GetValueOrDefault(Convert.ToInt32(l[1], CultureInfo.InvariantCulture), "Outros");
            if (!r.TryGetValue(nome, out var porMes)) r[nome] = porMes = [];
            porMes[(string)l[0]!] = Convert.ToInt32(l[2], CultureInfo.InvariantCulture);
        }
        return r;
    }

    internal static Dictionary<string, int> Somar(IReadOnlyDictionary<string, Dictionary<string, int>> porCategoria, PeriodoIndicadores p) =>
        p.Meses.ToDictionary(m => m, m => porCategoria.Values.Sum(c => c.GetValueOrDefault(m)));

    [GeneratedRegex(@"(?i)consulta\s+em\s+(.+?)(\s+-\s+|$)")]
    private static partial Regex RegexConsultaEm();

    [GeneratedRegex(@"(?i)ambulat[óo]rio\s+1[ªa]\s*vez\s*(?:-|em)\s*(.+?)(\s+-\s+|\s*\(|$)")]
    private static partial Regex RegexAmbulatorio();

    [GeneratedRegex(@"\s+-\s+|\s*\(|\s+de\s+|\s+do\s+|\s+da\s+")]
    private static partial Regex RegexCorteExame();
}

/// <summary>Resumo de tempos (mediana, P90, menor, maior) e tabela "top 15" — iguais ao relatório.</summary>
internal static class Tempo
{
    public static ResumoTempoDto Resumo(IEnumerable<int> dias)
    {
        var s = dias.OrderBy(d => d).ToList();
        if (s.Count == 0) return new ResumoTempoDto(0, null, null, null, null);
        var mediana = s.Count % 2 == 1 ? s[s.Count / 2] : (int)Math.Round((s[s.Count / 2 - 1] + s[s.Count / 2]) / 2.0, MidpointRounding.ToEven);
        return new ResumoTempoDto(s.Count, mediana, s[Math.Min(s.Count - 1, (int)(0.9 * s.Count))], s[0], s[^1]);
    }

    public static TabelaIndicadorDto Top<TItem>(
        IEnumerable<IGrouping<string, TItem>> grupos, Func<TItem, int> dias, string titulo, string rotuloN)
    {
        var linhas = grupos.Select(g => (g.Key, Itens: g.ToList()))
            .OrderByDescending(g => g.Itens.Count).Take(15)
            .Select(g =>
            {
                var r = Resumo(g.Itens.Select(dias));
                return (IReadOnlyList<string?>)[IndicadoresSisregCalculo.Truncar(g.Key), Formatar.Numero(r.N), Formatar.Numero(r.Mediana), Formatar.Numero(r.Menor), Formatar.Numero(r.Maior)];
            }).ToList();
        return new TabelaIndicadorDto(titulo, ["", rotuloN, "Mediana (dias)", "Menor", "Maior"], linhas, null);
    }
}
