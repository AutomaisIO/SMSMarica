using System.Globalization;
using SMSMais.Core.Regulacao.Indicadores.Dtos;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>
/// Indicadores do SISREG. Mesmas definições do relatório de 30/09/2026 (docs/regulacao/relatorio-2025-2026,
/// gerar_dados.py), reescritas para responder em segundos: agregação no banco e fila de fim de mês por
/// entradas − saídas acumuladas (uma varredura), em vez de uma consulta por mês.
/// </summary>
internal static class IndicadoresSisregCalculo
{
    private const string Fuso = "America/Sao_Paulo";

    // Código SISREG válido. O índice de codigo_solicitacao é PARCIAL (not null, <> '0000', não excluído):
    // busca por código repete esse predicado, senão o planner não usa o índice.
    private const string Base = """
        from smsmarica.solicitacao s
        where s.excluido_em is null and s.codigo_solicitacao is not null and s.codigo_solicitacao <> '0000'
          and s.codigo_solicitacao ~ '^[0-9]{9,10}$'
        """;

    public static async Task<IndicadoresRegulacaoDto> CalcularAsync(Sql sql, PeriodoIndicadores p, DateTime agora, CancellationToken ct)
    {
        var f = new Series(p);
        var secoes = new List<SecaoIndicadorDto>();
        var ini = p.Inicio;
        var fim = p.Fim;

        // --- vagas ofertadas (escalas) ------------------------------------------------------------------
        var vagas = await sql.LinhasAsync($"""
            with dias as (select d::date d from generate_series(@p0::date, @p1::date, interval '1 day') d)
            select to_char(d.d,'YYYY-MM'),
              sum(case when not e.agenda_local then coalesce(e.vagas_primeira_vez,0)+coalesce(e.vagas_reserva,0) else 0 end),
              sum(case when not e.agenda_local then coalesce(e.vagas_retorno,0) else 0 end),
              sum(case when e.agenda_local then coalesce(e.vagas_total,0) else 0 end),
              sum(coalesce(e.vagas_total,0))
            from dias d join smsmarica.sisreg_escala e
              on extract(dow from d.d) = e.dia_semana and d.d between e.vigencia_inicio and e.vigencia_fim
            where e.status in (1, 3) and not e.ausente
            group by 1
            """, ct, ini, fim);
        var vReg = Coluna(vagas, 1); var vRet = Coluna(vagas, 2); var vLoc = Coluna(vagas, 3); var vTot = Coluna(vagas, 4);

        var util = await sql.PorMesAsync($"""
            select to_char((s.data_agendada at time zone '{Fuso}')::date,'YYYY-MM'), count(*)
            {Base} and s.cancelado_em is null and s.data_agendada is not null
              and (s.data_agendada at time zone '{Fuso}')::date between @p0 and @p1
            group by 1
            """, ct, ini, fim);

        var ppi = await sql.LinhasAsync("""
            select to_char(competencia,'YYYY-MM'), sum(total), sum(usada) from smsmarica.sisreg_ppi_cota
            where competencia between @p0 and @p1 group by 1
            """, ct, ini, fim);
        var ppiTot = Coluna(ppi, 1); var ppiUsada = Coluna(ppi, 2);

        secoes.Add(new SecaoIndicadorDto("vagas", "Vagas disponibilizadas e utilizadas",
            "Oferta das escalas ambulatoriais cadastradas no SISREG para as unidades executantes da rede, comparada aos agendamentos com data de atendimento no mês.",
            false,
            [
                f.Serie("Vagas ofertadas (total)", vTot, SeloIndicador.Calculado,
                    "Soma, dia a dia, das escalas vigentes (ativas ou já expiradas) — retrato atual das escalas: alteração feita no próprio registro e bloqueio de agenda não aparecem.", destaque: true),
                f.Serie("para a regulação (1ª vez + reserva)", vReg, SeloIndicador.Calculado, subitem: true),
                f.Serie("retorno", vRet, SeloIndicador.Calculado, subitem: true),
                f.Serie("agenda local da unidade", vLoc, SeloIndicador.Calculado, subitem: true),
                f.Serie("Cotas PPI pactuadas (SISREG)", ppiTot, ppiTot.Count > 0 ? SeloIndicador.Oficial : SeloIndicador.Indisponivel,
                    "Consulta de PPI do SISREG. Em Maricá o valor é o mesmo em todas as competências — teto configurado, a confirmar se corresponde aos contratos.",
                    agregacao: AgregacaoIndicador.UltimoMes),
                f.Serie("Cotas PPI utilizadas", ppiUsada, ppiUsada.Count > 0 ? SeloIndicador.Oficial : SeloIndicador.Indisponivel),
                f.Serie("Vagas utilizadas (agendamentos)", util, SeloIndicador.Parcial,
                    "Agendamentos com data no mês. Marcações canceladas antes de agosto/2026 não constam da base espelhada.", destaque: true),
                f.Serie("Ocupação da oferta", f.Pct(util, vTot), SeloIndicador.Calculado, "Agendamentos ÷ vagas ofertadas.",
                    FormatoIndicador.Percentual, anual: f.PctAnual(util, vTot)),
            ],
            new GraficoIndicadorDto("barras", ["Vagas ofertadas (total)", "Vagas utilizadas (agendamentos)"]), null, [], null));

        // --- absenteísmo (lista oficial de faltas) --------------------------------------------------------
        // Mês só vale com a lista oficial lida por inteiro: todos os dias cobertos por janela concluída.
        var mesesComFaltas = (await sql.LinhasAsync("""
            with dias as (select d::date d from generate_series(@p0::date, @p1::date, interval '1 day') d),
            cob as (select distinct d.d from dias d join smsmarica.sisreg_indicador_coleta c
                    on c.coletor = 1 and c.status = 3 and d.d between c.janela_inicio and c.janela_fim)
            select to_char(d.d,'YYYY-MM') from dias d left join cob on cob.d = d.d
            group by 1 having count(*) = count(cob.d)
            """, ct, ini, fim)).Select(l => (string)l[0]!).ToHashSet();
        var faltas = await sql.LinhasAsync($"""
            select to_char(f.data_execucao,'YYYY-MM'), count(*), count(x.ok)
            from smsmarica.sisreg_falta f
            left join lateral (
                select 1 ok from smsmarica.solicitacao s
                where s.codigo_solicitacao = f.codigo_solicitacao and s.codigo_solicitacao is not null
                  and s.codigo_solicitacao <> '0000' and s.excluido_em is null
                  and (s.data_agendada at time zone '{Fuso}')::date = f.data_execucao
                limit 1) x on true
            where f.data_execucao between @p0 and @p1
            group by 1
            """, ct, ini, fim);
        var fTot = new Dictionary<string, int>(); var fCas = new Dictionary<string, int>();
        foreach (var m in mesesComFaltas) { fTot[m] = 0; fCas[m] = 0; }
        foreach (var l in faltas)
        {
            var m = (string)l[0]!;
            if (!mesesComFaltas.Contains(m)) continue;
            fTot[m] = Convert.ToInt32(l[1], CultureInfo.InvariantCulture);
            fCas[m] = Convert.ToInt32(l[2], CultureInfo.InvariantCulture);
        }
        var temFaltas = fTot.Count > 0;
        var atendidas = p.Meses.Where(m => fCas.ContainsKey(m) && util.ContainsKey(m)).ToDictionary(m => m, m => util[m] - fCas[m]);
        secoes.Add(new SecaoIndicadorDto("absenteismo", "Absenteísmo",
            "Faltas pela Consulta de Absenteísmo por Unidade de Saúde do SISREG (agendamentos em que a unidade executante registrou falta), casadas com os agendamentos do mês. Agendamento em que a unidade não apontou nem chegada nem falta não entra como falta.",
            false,
            [
                f.Serie("Agendamentos no mês", util, SeloIndicador.Parcial),
                f.Serie("Faltas (lista oficial do SISREG)", fTot, temFaltas ? SeloIndicador.Oficial : SeloIndicador.Indisponivel,
                    temFaltas ? null : "A lista oficial de faltas ainda não foi coletada para o período (coleta em SISREG → Configuração).", destaque: true),
                f.Serie("Absenteísmo", f.Pct(fCas, util), temFaltas ? SeloIndicador.Calculado : SeloIndicador.Indisponivel,
                    "Faltas casadas com os agendamentos (mesmo código e mesma data) ÷ agendamentos do mês.",
                    FormatoIndicador.Percentual, destaque: true, anual: f.PctAnual(fCas, util)),
                f.Serie("Atendidos (agendamentos − faltas)", atendidas, temFaltas ? SeloIndicador.Calculado : SeloIndicador.Indisponivel),
                f.Serie("Faltas sem agendamento correspondente na base", fTot.ToDictionary(kv => kv.Key, kv => kv.Value - fCas[kv.Key]),
                    SeloIndicador.Calculado, "Linha de auditoria: falta oficial cujo agendamento não está na base espelhada (em geral, remarcado ou de unidade fora da varredura)."),
            ],
            new GraficoIndicadorDto("barras", ["Faltas (lista oficial do SISREG)"]), null, [], null));

        // --- quantitativo regulado ------------------------------------------------------------------------
        var reg = await sql.PorMesECategoriaAsync($"""
            select to_char(s.data_regulacao,'YYYY-MM'),
                   case when s.categoria = 1 then 'Consultas' else 'Exames e demais procedimentos' end, count(*)
            {Base} and s.data_regulacao between @p0 and @p1
            group by 1, 2
            """, ct, ini, fim);
        var regTot = p.Meses.ToDictionary(m => m, m => reg.Values.Sum(c => c.GetValueOrDefault(m)));
        var catsReg = new[] { "Consultas", "Exames e demais procedimentos" }.Where(reg.ContainsKey).ToList();
        secoes.Add(new SecaoIndicadorDto("regulados", "Quantitativo regulado de consultas e exames",
            "Solicitações autorizadas (com data de regulação no mês), por tipo de procedimento. A base histórica distingue apenas consulta × demais procedimentos (exames, métodos gráficos, endoscopias, cirurgias).",
            false,
            [
                f.Serie("Total regulado", regTot, SeloIndicador.Parcial,
                    "Autorizações cuja marcação foi cancelada antes de agosto/2026 não constam da base espelhada.", destaque: true),
                .. catsReg.Select(c => f.Serie(c, reg[c], SeloIndicador.Parcial, subitem: true)),
            ],
            new GraficoIndicadorDto("empilhado", catsReg), null, [], null));

        // --- fila no fim do mês e solicitações no mês -----------------------------------------------------
        // fila(m) = Σ entradas até m − Σ saídas até m, sobre quem de fato esperou. Uma varredura por fonte.
        var fila = await sql.LinhasAsync($"""
            with mov as (
                select s.data_solicitacao e, s.data_regulacao x {Base}
                  and s.data_solicitacao is not null and s.data_regulacao > s.data_solicitacao
                union all
                select f.data_solicitacao, (f.saiu_em at time zone '{Fuso}')::date from smsmarica.sisreg_fila_pendente f
                where f.data_solicitacao is not null and (f.saiu_em is null or f.saiu_para = 2)
                union all
                select d.data_solicitacao, d.data_desfecho from smsmarica.sisreg_solicitacao_desfecho d
                where d.data_solicitacao is not null and d.data_desfecho is not null and d.data_desfecho > d.data_solicitacao
                  and not exists (select 1 from smsmarica.sisreg_fila_pendente f2 where f2.codigo_solicitacao = d.codigo_solicitacao)
                  and not exists (select 1 from smsmarica.solicitacao s2 where s2.codigo_solicitacao = d.codigo_solicitacao
                                  and s2.codigo_solicitacao is not null and s2.codigo_solicitacao <> '0000' and s2.excluido_em is null)
            ),
            ent as (select date_trunc('month', e)::date mes, count(*) n from mov group by 1),
            sai as (select date_trunc('month', x)::date mes, count(*) n from mov where x is not null group by 1),
            meses as (select generate_series(@p0::date, @p1::date, interval '1 month')::date mes)
            select to_char(m.mes,'YYYY-MM'),
                   coalesce((select sum(n) from ent where ent.mes <= m.mes), 0)
                 - coalesce((select sum(n) from sai where sai.mes <= m.mes), 0)
            from meses m
            """, ct, ini, fim);
        var filaFim = fila.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[1], CultureInfo.InvariantCulture));
        var solic = await sql.PorMesAsync($"""
            with c as (
                select s.codigo_solicitacao cod, s.data_solicitacao ds {Base} and s.data_solicitacao between @p0 and @p1
                union select f.codigo_solicitacao, f.data_solicitacao from smsmarica.sisreg_fila_pendente f
                      where f.data_solicitacao between @p0 and @p1
                union select d.codigo_solicitacao, d.data_solicitacao from smsmarica.sisreg_solicitacao_desfecho d
                      where d.data_solicitacao between @p0 and @p1)
            select to_char(ds,'YYYY-MM'), count(distinct cod) from c group by 1
            """, ct, ini, fim);
        secoes.Add(new SecaoIndicadorDto("fila", "Fila e solicitações",
            "Pacientes aguardando regulação no último dia de cada mês e solicitações registradas no mês.",
            false,
            [
                f.Serie("Pacientes em fila no fim do mês", filaFim, SeloIndicador.Parcial,
                    "Reconstruída: quem ainda aguarda e já tinha pedido naquela data + quem foi autorizado ou saiu da fila depois dela. Saídas sem agendamento sem data conhecida (negadas e canceladas antes de 10/09/2026) ficam de fora — o número é um piso.",
                    destaque: true, agregacao: AgregacaoIndicador.UltimoMes),
                f.Serie("Solicitações registradas no mês", solic, SeloIndicador.Parcial,
                    "Códigos distintos pela data da solicitação (agendados + fila + devolvidas/negadas/canceladas coletadas)."),
            ],
            new GraficoIndicadorDto("barras", ["Pacientes em fila no fim do mês"]), null, [], null));

        // --- desfechos: atendidas, canceladas, excluídas + motivos ----------------------------------------
        var canc = await CanceladasPorMesAsync(sql, ini, fim, ct);
        var exc = await sql.PorMesECategoriaAsync("""
            select to_char(data_solicitacao,'YYYY-MM'), situacao::text, count(distinct codigo_solicitacao)
            from smsmarica.sisreg_solicitacao_desfecho where data_solicitacao between @p0 and @p1 group by 1, 2
            """, ct, ini, fim);
        var excTot = p.Meses.ToDictionary(m => m, m => exc.Values.Sum(c => c.GetValueOrDefault(m)));
        var unidadesLidas = (await sql.LinhasAsync("""
            select count(distinct escopo) from smsmarica.sisreg_indicador_coleta
            where coletor = 3 and status = 3 and escopo ~ '^[0-9]{7}$'
            """, ct))[0][0];
        var temDesfechos = exc.Count > 0;
        var seloDesf = temDesfechos ? SeloIndicador.Oficial : SeloIndicador.Indisponivel;
        var rotSit = new Dictionary<string, string> { ["4"] = "devolvidas pelo regulador", ["6"] = "negadas pelo regulador", ["3"] = "canceladas antes do agendamento" };
        var motivos = await MotivosAsync(sql, ini, fim, canc, ct);
        secoes.Add(new SecaoIndicadorDto("desfechos", "Atendidas, canceladas e excluídas",
            "Desfecho das solicitações: atendidas (agendamento cumprido), marcações canceladas (pela data do cancelamento) e solicitações excluídas da fila sem agendamento (pela data da solicitação).",
            false,
            [
                f.Serie("Atendidas (agendamentos − faltas)", atendidas, temFaltas ? SeloIndicador.Calculado : SeloIndicador.Indisponivel, destaque: true),
                f.Serie("Marcações canceladas", canc, canc.Count > 0 ? SeloIndicador.Oficial : SeloIndicador.Indisponivel,
                    "Consulta de Marcações Canceladas do SISREG, pela data do cancelamento.", destaque: true),
                f.Serie("Excluídas da fila sem agendamento", excTot, seloDesf,
                    temDesfechos ? $"Coletadas por unidade solicitante ({Convert.ToInt32(unidadesLidas ?? 0, CultureInfo.InvariantCulture)} unidades lidas)." : "Coleta por unidade solicitante ainda não rodou.",
                    destaque: true),
                .. new[] { "4", "6", "3" }.Where(exc.ContainsKey).Select(sit => f.Serie(rotSit[sit], exc[sit], seloDesf, subitem: true)),
            ],
            new GraficoIndicadorDto("barras", ["Marcações canceladas", "Excluídas da fila sem agendamento"]), motivos, [], null));

        // --- tempo de espera ------------------------------------------------------------------------------
        secoes.Add(await EsperaAsync(sql, f, p, ct));

        secoes.Add(new SecaoIndicadorDto("judicial", "Demandas judicializadas",
            "O SISREG não possui marcador de solicitação por mandado judicial — nem na solicitação, nem na fila do regulador. O indicador depende de registro próprio da Secretaria (ex.: lista da Procuradoria).",
            true, [], null, null, [], null));

        var cob = (await sql.LinhasAsync($"""
            select (select max(s.criado_em) {Base}), (select max(ultimo_visto_em) from smsmarica.sisreg_fila_pendente),
                   (select max(lido_em) from smsmarica.sisreg_indicador_coleta where status = 3)
            """, ct))[0];
        var cobertura = new List<string>
        {
            $"Agendamentos: espelho do Arquivo de Agendamentos do SISREG, última carga em {Formatar.Data(cob[0]) ?? "—"}.",
            $"Fila de espera: lida do SISREG diariamente desde 10/09/2026 (última leitura {Formatar.Data(cob[1]) ?? "—"}).",
            cob[2] is null
                ? "Faltas oficiais, cotas PPI, marcações canceladas e desfechos pré-agendamento: coleta ainda não executada (SISREG → Configuração)."
                : $"Faltas oficiais, cotas PPI, marcações canceladas e desfechos pré-agendamento: última coleta em {Formatar.Data(cob[2])}.",
        };
        return new IndicadoresRegulacaoDto(FonteIndicadorRegulacao.Sisreg, "SISREG",
            "SISREG III — Sistema Nacional de Regulação (Ministério da Saúde)", p.Meses, agora, cobertura, secoes);
    }

    /// <summary>Total oficial por mês: o total DECLARADO pela tela — da janela mensal (coletor/carga) ou, sem
    /// ela, a soma dos dias fechados pela conciliação diária, só quando TODOS os dias do mês fecharam (mês
    /// com buraco não tem total oficial). Sem declarado, conta as linhas gravadas.</summary>
    private static async Task<Dictionary<string, int>> CanceladasPorMesAsync(Sql sql, DateOnly ini, DateOnly fim, CancellationToken ct)
    {
        var declarado = await sql.PorMesAsync("""
            with c as (
                select janela_inicio, janela_fim, linhas from smsmarica.sisreg_indicador_coleta
                where coletor = 2 and status = 3 and linhas is not null and janela_inicio between @p0 and @p1
                  and escopo <> 'amostra' -- a amostra de motivos conta o que LEU, não o total do mês
                  and date_trunc('month', janela_inicio) = date_trunc('month', janela_fim)),
            mensal as (
                select date_trunc('month', janela_inicio)::date m, max(linhas) n from c
                where janela_inicio = date_trunc('month', janela_inicio)::date
                  and janela_fim = (date_trunc('month', janela_inicio) + interval '1 month - 1 day')::date
                group by 1),
            diario as (
                select date_trunc('month', janela_inicio)::date m, sum(linhas) n, count(distinct janela_inicio) dias
                from c where janela_inicio = janela_fim group by 1)
            select to_char(coalesce(mensal.m, diario.m),'YYYY-MM'), coalesce(mensal.n, diario.n)::int
            from mensal full join diario on diario.m = mensal.m
            where mensal.m is not null
               or diario.dias = extract(day from (diario.m + interval '1 month - 1 day'))
            """, ct, ini, fim);
        var linhas = await sql.PorMesAsync($"""
            select to_char((cancelado_em at time zone '{Fuso}')::date,'YYYY-MM'), count(*) from smsmarica.sisreg_marcacao_cancelada
            where (cancelado_em at time zone '{Fuso}')::date between @p0 and @p1 group by 1
            """, ct, ini, fim);
        var r = new Dictionary<string, int>(linhas);
        foreach (var (m, n) in declarado) r[m] = Math.Max(n, r.GetValueOrDefault(m));
        return r;
    }

    /// <summary>Motivos por categoria e ano. Quando as linhas lidas são uma AMOSTRA (bem menos que o total
    /// oficial), mostra o % estimado e a estimativa = % × total oficial do ano (ver <see cref="EstimarMotivos"/>).</summary>
    private static async Task<TabelaIndicadorDto?> MotivosAsync(
        Sql sql, DateOnly ini, DateOnly fim, IReadOnlyDictionary<string, int> totalPorMes, CancellationToken ct)
    {
        var linhas = await sql.LinhasAsync($"""
            select to_char((cancelado_em at time zone '{Fuso}')::date,'YYYY-MM'), coalesce(justificativa,'')
            from smsmarica.sisreg_marcacao_cancelada
            where (cancelado_em at time zone '{Fuso}')::date between @p0 and @p1
            """, ct, ini, fim);
        if (linhas.Count == 0) return null;

        var e = EstimarMotivos(
            linhas.Select(l => ((string)l[0]!, MotivoCancelamento.Categoria((string?)l[1]))), totalPorMes);
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var colunas = new List<string> { "Motivo" };
        foreach (var a in e.Anos) colunas.AddRange(e.Amostra ? new[] { $"% {a}", $"Estimativa {a}" } : new[] { $"{a}" });
        var corpo = e.Categorias.Select(cat =>
        {
            var l = new List<string?> { cat };
            foreach (var a in e.Anos)
            {
                if (e.Amostra)
                {
                    var pct = e.Percentual.GetValueOrDefault((a, cat));
                    l.Add(pct.ToString("0.0", ptBr) + "%");
                    l.Add(Formatar.Numero(Math.Round(pct * e.TotalAno.GetValueOrDefault(a) / 100m)));
                }
                else l.Add(Formatar.Numero(e.Lidas.GetValueOrDefault((a, cat))));
            }
            return (IReadOnlyList<string?>)l;
        }).ToList();
        var nota = e.Amostra
            ? $"Justificativa registrada no cancelamento, agrupada por categoria (texto livre não reproduzido). AMOSTRA de {e.LidasTotal:N0} cancelamentos, lidos em páginas espalhadas de cada mês; o % de cada mês pesa pelo total oficial de canceladas daquele mês; estimativa = % × total oficial do ano."
            : "Justificativa registrada no cancelamento, agrupada por categoria (texto livre não reproduzido).";
        return new TabelaIndicadorDto("Motivos das marcações canceladas no SISREG", colunas, corpo, nota);
    }

    /// <summary>Resultado de <see cref="EstimarMotivos"/>: % por (ano, categoria), linhas lidas e totais oficiais.</summary>
    internal sealed record EstimativaMotivos(
        IReadOnlyList<int> Anos,
        IReadOnlyList<string> Categorias,
        bool Amostra,
        IReadOnlyDictionary<(int Ano, string Categoria), decimal> Percentual,
        IReadOnlyDictionary<(int Ano, string Categoria), int> Lidas,
        IReadOnlyDictionary<int, int> TotalAno,
        int LidasTotal);

    /// <summary>
    /// Motivos por ano a partir de linhas que podem ser AMOSTRA. A amostra do SISREG é de páginas espalhadas
    /// (≈ o mesmo tanto de linhas em todo mês), então juntar o ano inteiro daria o mesmo peso a um mês de
    /// 1.200 e a um de 2.400 cancelamentos — e um mês lido INTEIRO no meio de meses amostrados engoliria o
    /// ano. Aqui cada mês pesa pelo seu total oficial: estimativa do mês = contagem da categoria ÷ linhas
    /// lidas no mês × total oficial do mês; o % do ano é a soma dessas estimativas sobre a soma delas.
    /// </summary>
    internal static EstimativaMotivos EstimarMotivos(
        IEnumerable<(string Mes, string Categoria)> linhas, IReadOnlyDictionary<string, int> totalPorMes)
    {
        var porMes = linhas.GroupBy(l => l.Mes)
            .ToDictionary(g => g.Key, g => g.GroupBy(l => l.Categoria).ToDictionary(c => c.Key, c => c.Count()));
        static int Ano(string mes) => int.Parse(mes[..4], CultureInfo.InvariantCulture);

        var estimada = new Dictionary<(int, string), decimal>();
        var lidas = new Dictionary<(int, string), int>();
        var amostra = false;
        foreach (var (mes, cats) in porMes)
        {
            var n = cats.Values.Sum();
            var total = totalPorMes.GetValueOrDefault(mes);
            if (total > 0 && n < 0.9 * total) amostra = true;
            var peso = total > n ? (decimal)total / n : 1m;
            foreach (var (cat, c) in cats)
            {
                var k = (Ano(mes), cat);
                estimada[k] = estimada.GetValueOrDefault(k) + c * peso;
                lidas[k] = lidas.GetValueOrDefault(k) + c;
            }
        }

        var anos = porMes.Keys.Select(Ano).Distinct().Order().ToList();
        var somaAno = anos.ToDictionary(a => a, a => estimada.Where(kv => kv.Key.Item1 == a).Sum(kv => kv.Value));
        var percentual = estimada.ToDictionary(kv => kv.Key,
            kv => somaAno[kv.Key.Item1] > 0 ? Math.Round(100m * kv.Value / somaAno[kv.Key.Item1], 1) : 0m);
        var totalAno = anos.ToDictionary(a => a, a => totalPorMes
            .Where(kv => kv.Key.StartsWith(a.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            .Sum(kv => kv.Value));
        var categorias = estimada.GroupBy(kv => kv.Key.Item2)
            .OrderByDescending(g => g.Sum(kv => kv.Value)).Select(g => g.Key).ToList();
        return new EstimativaMotivos(anos, categorias, amostra, percentual, lidas, totalAno, lidas.Values.Sum());
    }

    private static async Task<SecaoIndicadorDto> EsperaAsync(Sql sql, Series f, PeriodoIndicadores p, CancellationToken ct)
    {
        // Duas esperas: até o atendimento marcado (a que o paciente sente) e em fila só de quem aguardou
        // regulação (autorização em dia posterior ao pedido) — a agenda direta puxaria a mediana para ~0.
        var mensal = await sql.LinhasAsync($"""
            with e as (
                select to_char(s.data_regulacao,'YYYY-MM') mes,
                       (s.data_regulacao - s.data_solicitacao) fila_d,
                       ((s.data_agendada at time zone '{Fuso}')::date - s.data_solicitacao) atend_d
                {Base} and s.data_regulacao between @p0 and @p1 and s.data_solicitacao is not null
                  and s.data_regulacao >= s.data_solicitacao)
            select mes,
                   percentile_disc(0.5) within group (order by atend_d) filter (where atend_d >= 0),
                   percentile_disc(0.9) within group (order by atend_d) filter (where atend_d >= 0),
                   min(atend_d) filter (where atend_d >= 0), max(atend_d) filter (where atend_d >= 0),
                   percentile_disc(0.5) within group (order by fila_d) filter (where fila_d > 0),
                   max(fila_d) filter (where fila_d > 0),
                   count(*) filter (where fila_d = 0), count(*)
            from e group by mes
            """, ct, p.Inicio, p.Fim);
        var anual = await sql.LinhasAsync($"""
            with e as (
                select extract(year from s.data_regulacao)::int::text ano,
                       (s.data_regulacao - s.data_solicitacao) fila_d,
                       ((s.data_agendada at time zone '{Fuso}')::date - s.data_solicitacao) atend_d
                {Base} and s.data_regulacao between @p0 and @p1 and s.data_solicitacao is not null
                  and s.data_regulacao >= s.data_solicitacao)
            select ano,
                   percentile_disc(0.5) within group (order by atend_d) filter (where atend_d >= 0),
                   percentile_disc(0.9) within group (order by atend_d) filter (where atend_d >= 0),
                   min(atend_d) filter (where atend_d >= 0), max(atend_d) filter (where atend_d >= 0),
                   percentile_disc(0.5) within group (order by fila_d) filter (where fila_d > 0),
                   max(fila_d) filter (where fila_d > 0),
                   count(*) filter (where fila_d = 0), count(*)
            from e group by ano
            """, ct, p.Inicio, p.Fim);

        Dictionary<string, decimal?> Col(List<object?[]> ls, int i) => ls.ToDictionary(l => (string)l[0]!, l => Formatar.Dec(l[i]));
        var mesmoDia = mensal.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[7], CultureInfo.InvariantCulture));
        var total = mensal.ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[8], CultureInfo.InvariantCulture));
        var pctMesmoDiaAnual = anual.ToDictionary(l => (string)l[0]!, l =>
            Convert.ToInt32(l[8], CultureInfo.InvariantCulture) > 0
                ? Math.Round(100m * Convert.ToInt32(l[7], CultureInfo.InvariantCulture) / Convert.ToInt32(l[8], CultureInfo.InvariantCulture), 1)
                : (decimal?)null);

        var tabelas = new List<TabelaIndicadorDto>();
        foreach (var ano in p.Anos)
        {
            tabelas.Add(await TopAsync(sql, p, ano, porEspecialidade: true, ct));
        }
        foreach (var ano in p.Anos)
        {
            tabelas.Add(await TopAsync(sql, p, ano, porEspecialidade: false, ct));
        }
        var aguard = await sql.LinhasAsync("""
            select coalesce(nullif(trim(procedimento_nome),''),'(sem nome)'), count(*),
                   percentile_disc(0.5) within group (order by current_date - data_solicitacao),
                   max(current_date - data_solicitacao)
            from smsmarica.sisreg_fila_pendente where saiu_em is null and data_solicitacao is not null
            group by 1 order by 2 desc limit 15
            """, ct);
        tabelas.Add(new TabelaIndicadorDto("Quem ainda aguarda (fila atual), 15 procedimentos com mais pacientes",
            ["", "Aguardando", "Mediana (dias)", "Maior (dias)"],
            aguard.Select(l => (IReadOnlyList<string?>)[Truncar((string?)l[0]), Formatar.Numero(l[1]), Formatar.Numero(l[2]), Formatar.Numero(l[3])]).ToList(), null));

        return new SecaoIndicadorDto("espera", "Tempo de espera",
            "Espera até o atendimento: dias entre a solicitação e a data do atendimento marcado, das solicitações reguladas no mês. Espera em fila: dias entre a solicitação e a autorização, só de quem aguardou regulação (autorizações no mesmo dia do pedido — agenda direta — aparecem à parte).",
            false,
            [
                f.Serie("Espera até o atendimento — mediana (dias)", Col(mensal, 1), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, destaque: true, anual: Col(anual, 1)),
                f.Serie("Espera até o atendimento — 90% até (dias)", Col(mensal, 2), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Col(anual, 2)),
                f.Serie("Espera até o atendimento — menor (dias)", Col(mensal, 3), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Col(anual, 3)),
                f.Serie("Espera até o atendimento — maior (dias)", Col(mensal, 4), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Col(anual, 4)),
                f.Serie("Espera em fila (quem aguardou) — mediana (dias)", Col(mensal, 5), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, destaque: true, anual: Col(anual, 5)),
                f.Serie("Espera em fila (quem aguardou) — maior (dias)", Col(mensal, 6), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Col(anual, 6)),
                f.Serie("Autorizadas no mesmo dia do pedido", f.Pct(mesmoDia, total), SeloIndicador.Calculado,
                    "Percentual das autorizações do mês feitas no mesmo dia da solicitação.", FormatoIndicador.Percentual, anual: pctMesmoDiaAnual),
            ],
            new GraficoIndicadorDto("barras", ["Espera até o atendimento — mediana (dias)", "Espera em fila (quem aguardou) — mediana (dias)"]),
            null, tabelas, null);
    }

    private static async Task<TabelaIndicadorDto> TopAsync(Sql sql, PeriodoIndicadores p, string ano, bool porEspecialidade, CancellationToken ct)
    {
        var chave = porEspecialidade
            ? """
              case when s.categoria = 1
                   then 'Consulta — ' || initcap(coalesce(nullif(trim(s.especialidade_texto),''),
                        nullif(trim(split_part(regexp_replace(coalesce(s.procedimento_texto,''), '^.*?CONSULTA\s+EM\s+', '', 'i'), ' - ', 1)),''),
                        '(sem nome)'))
                   else 'Exame/procedimento — ' || initcap(coalesce(nullif(trim(split_part(regexp_replace(coalesce(s.procedimento_texto,''),
                        '^\s*GRUPO\s*-\s*', '', 'i'), ' - ', 1)),''), '(sem nome)'))
              end
              """
            : "coalesce(nullif(trim(s.procedimento_texto),''),'(sem nome)')";
        var linhas = await sql.LinhasAsync($"""
            with e as (
                select {chave} k, ((s.data_agendada at time zone '{Fuso}')::date - s.data_solicitacao) d
                {Base} and s.data_regulacao between @p0 and @p1 and extract(year from s.data_regulacao)::int = @p2
                  and s.data_solicitacao is not null and s.data_regulacao >= s.data_solicitacao
                  and s.data_agendada is not null)
            select k, count(*), percentile_disc(0.5) within group (order by d), min(d), max(d)
            from e where d >= 0 group by k order by 2 desc limit 15
            """, ct, p.Inicio, p.Fim, int.Parse(ano, CultureInfo.InvariantCulture));
        var ateHoje = p.Meses.Last().StartsWith(ano, StringComparison.Ordinal) && !p.Meses.Last().EndsWith("-12", StringComparison.Ordinal)
            ? $" até {MesCurto(p.Meses.Last())}" : string.Empty;
        return new TabelaIndicadorDto(
            porEspecialidade ? $"Por especialidade — {ano}{ateHoje} (dias até o atendimento)" : $"Por procedimento — {ano}{ateHoje}, 15 mais regulados (dias até o atendimento)",
            ["", "Regulados", "Mediana (dias)", "Menor", "Maior"],
            linhas.Select(l => (IReadOnlyList<string?>)[Truncar((string?)l[0]), Formatar.Numero(l[1]), Formatar.Numero(l[2]), Formatar.Numero(l[3]), Formatar.Numero(l[4])]).ToList(),
            null);
    }

    internal static string MesCurto(string mes)
    {
        string[] nomes = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];
        return nomes[int.Parse(mes[5..], CultureInfo.InvariantCulture) - 1];
    }

    internal static string? Truncar(string? s) => s is null ? null : s.Length <= 70 ? s : s[..70];

    private static Dictionary<string, int> Coluna(List<object?[]> linhas, int i) =>
        linhas.Where(l => l[i] is not null).ToDictionary(l => (string)l[0]!, l => Convert.ToInt32(l[i], CultureInfo.InvariantCulture));
}
