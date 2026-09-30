using System.Globalization;
using SMSMais.Core.Regulacao.Indicadores.Dtos;

namespace SMSMais.Core.Regulacao.Indicadores;

/// <summary>
/// Indicadores do ESUS de São Gonçalo (ADR-0063): espelho da fila e dos agendados do município no ESUS de SG.
/// Traz entrada e saída da fila — a fila de fim de mês sai inteira —, mas não comparecimento, oferta nem
/// motivo de quem sai sem agendamento. Judicial = prioridade "Mandado judicial".
/// </summary>
internal static class IndicadoresEsusSgCalculo
{
    private const string S = "smsmarica.esussg_solicitacao";

    public static async Task<IndicadoresRegulacaoDto> CalcularAsync(Sql sql, PeriodoIndicadores p, DateTime agora, CancellationToken ct)
    {
        var f = new Series(p);
        var tipos = new Dictionary<int, string> { [1] = "Consultas", [2] = "Exames" };
        var secoes = new List<SecaoIndicadorDto>();
        const string dataRegulacao = "coalesce(s.agendamento_cadastrado_em, s.data_saida_fila)";

        var ag = await PorTipoAsync(sql, $"""
            select to_char(s.data_agendada,'YYYY-MM'), s.tipo, count(*) from {S} s
            where s.excluido_em is null and s.data_agendada between @p0 and @p1 group by 1, 2
            """, tipos, p, ct);
        var agTot = IndicadoresExternoCalculo.Somar(ag, p);
        secoes.Add(new SecaoIndicadorDto("vagas", "Vagas disponibilizadas e utilizadas",
            "A oferta de vagas do ESUS de São Gonçalo (cotas da PPI) pertence ao município executor e não é exibida ao município solicitante. Utilização = agendamentos com data de atendimento no mês.",
            false,
            [
                f.Serie("Vagas ofertadas", new Dictionary<string, int>(), SeloIndicador.Indisponivel, "O ESUS de São Gonçalo não expõe a oferta ao município solicitante."),
                f.Serie("Vagas utilizadas (agendamentos no mês)", agTot, SeloIndicador.Oficial, "Pela data do atendimento agendado.", destaque: true),
                .. new[] { "Consultas", "Exames" }.Where(ag.ContainsKey).Select(t => f.Serie(t, ag[t], SeloIndicador.Oficial, subitem: true)),
            ],
            new GraficoIndicadorDto("barras", ["Vagas utilizadas (agendamentos no mês)"]), null, [], null));
        secoes.Add(new SecaoIndicadorDto("absenteismo", "Absenteísmo",
            "O ESUS de São Gonçalo não informa ao município solicitante se o paciente compareceu ao atendimento agendado.",
            true, [], null, null, [], null));

        var reg = await PorTipoAsync(sql, $"""
            select to_char({dataRegulacao},'YYYY-MM'), s.tipo, count(*) from {S} s
            where s.excluido_em is null and s.situacao = 3 and {dataRegulacao} between @p0 and @p1 group by 1, 2
            """, tipos, p, ct);
        var regTot = IndicadoresExternoCalculo.Somar(reg, p);
        var catsReg = new[] { "Consultas", "Exames" }.Where(reg.ContainsKey).ToList();
        secoes.Add(new SecaoIndicadorDto("regulados", "Quantitativo regulado de consultas e exames",
            "Solicitações agendadas pela regulação de São Gonçalo no mês (data em que o agendamento foi feito).", false,
            [f.Serie("Total regulado", regTot, SeloIndicador.Oficial, destaque: true),
             .. catsReg.Select(t => f.Serie(t, reg[t], SeloIndicador.Oficial, subitem: true))],
            new GraficoIndicadorDto("empilhado", catsReg), null, [], null));

        var fila = await sql.PorMesAsync($"""
            with meses as (select (date_trunc('month', g) + interval '1 month - 1 day')::date fim
                           from generate_series(@p0::date, @p1::date, interval '1 month') g)
            select to_char(m.fim,'YYYY-MM'),
                   (select count(*) from {S} s where s.excluido_em is null and s.data_entrada_fila <= m.fim
                      and (s.data_saida_fila is null or s.data_saida_fila > m.fim))
            from meses m
            """, ct, p.Inicio, p.Fim);
        var so = await PorTipoAsync(sql, $"""
            select to_char(s.data_entrada_fila,'YYYY-MM'), s.tipo, count(*) from {S} s
            where s.excluido_em is null and s.data_entrada_fila between @p0 and @p1 group by 1, 2
            """, tipos, p, ct);
        var soTot = IndicadoresExternoCalculo.Somar(so, p);
        secoes.Add(new SecaoIndicadorDto("fila", "Fila e solicitações",
            "Pacientes na fila do ESUS de São Gonçalo no último dia do mês (entraram até a data e ainda não tinham saído) e solicitações que entraram na fila no mês.",
            false,
            [
                f.Serie("Solicitações em fila no fim do mês", fila, SeloIndicador.Parcial,
                    "Reconstruída pelas datas de entrada e saída da fila. Quem saiu da fila sem agendamento antes do primeiro espelho já não aparece no ESUS — o número é um piso.",
                    destaque: true, agregacao: AgregacaoIndicador.UltimoMes),
                f.Serie("Solicitações registradas no mês", soTot, SeloIndicador.Parcial, "Pela data de entrada na fila; exclusões anteriores ao primeiro espelho não aparecem."),
                .. new[] { "Consultas", "Exames" }.Where(so.ContainsKey).Select(t => f.Serie(t, so[t], SeloIndicador.Parcial, subitem: true)),
            ],
            new GraficoIndicadorDto("barras", ["Solicitações em fila no fim do mês"]), null, [], null));

        secoes.Add(new SecaoIndicadorDto("desfechos", "Atendidas, canceladas e excluídas",
            "O ESUS de São Gonçalo mostra ao município só duas listas — fila e agendados. Quem sai das duas sem agendamento (exclusão, cancelamento, transferência) some sem motivo visível; o espelho registra essas saídas a partir da primeira varredura. Canceladas, excluídas e motivos não estão disponíveis para o período anterior.",
            false,
            [
                f.Serie("Agendadas (saíram da fila com agendamento)", regTot, SeloIndicador.Oficial, destaque: true),
                f.Serie("Canceladas / excluídas", new Dictionary<string, int>(), SeloIndicador.Indisponivel, "O ESUS não exibe cancelamento nem exclusão ao município solicitante."),
            ],
            new GraficoIndicadorDto("barras", ["Agendadas (saíram da fila com agendamento)"]), null, [], null));

        // espera
        var esp = await sql.LinhasAsync($"""
            select to_char({dataRegulacao},'YYYY-MM'), ({dataRegulacao} - s.data_entrada_fila), (s.data_agendada - s.data_entrada_fila),
                   coalesce(s.recurso,'(sem recurso)')
            from {S} s where s.excluido_em is null and s.situacao = 3 and s.data_entrada_fila is not null
              and {dataRegulacao} between @p0 and @p1
            """, ct, p.Inicio, p.Fim);
        var itens = esp.Select(l => (Mes: (string)l[0]!, Fila: l[1] is null ? (int?)null : Convert.ToInt32(l[1], CultureInfo.InvariantCulture),
            Atend: l[2] is null ? (int?)null : Convert.ToInt32(l[2], CultureInfo.InvariantCulture), Recurso: (string)l[3]!)).ToList();
        var comAtend = itens.Where(i => i.Atend is >= 0).Select(i => (i.Mes, Dias: i.Atend!.Value, i.Recurso)).ToList();
        var comFila = itens.Where(i => i.Fila is >= 0).Select(i => (i.Mes, Dias: i.Fila!.Value)).ToList();
        Dictionary<string, decimal?> Mes(IEnumerable<(string Mes, int Dias)> xs, Func<ResumoTempoDto, int?> sel) =>
            xs.GroupBy(x => x.Mes).ToDictionary(g => g.Key, g => (decimal?)sel(Tempo.Resumo(g.Select(x => x.Dias))));
        Dictionary<string, decimal?> Ano(IEnumerable<(string Mes, int Dias)> xs, Func<ResumoTempoDto, int?> sel) =>
            xs.GroupBy(x => x.Mes[..4]).ToDictionary(g => g.Key, g => (decimal?)sel(Tempo.Resumo(g.Select(x => x.Dias))));
        var at = comAtend.Select(x => (x.Mes, x.Dias)).ToList();

        var tabelas = new List<TabelaIndicadorDto>();
        foreach (var ano in p.Anos)
            tabelas.Add(Tempo.Top(comAtend.Where(x => x.Mes.StartsWith(ano, StringComparison.Ordinal)).GroupBy(x => x.Recurso), x => x.Dias,
                $"Por recurso — {ano} (dias até o atendimento)", "Agendados"));
        var aguard = await sql.LinhasAsync($"""
            select coalesce(s.recurso,'(sem recurso)'), count(*), percentile_disc(0.5) within group (order by current_date - s.data_entrada_fila),
                   max(current_date - s.data_entrada_fila)
            from {S} s where s.excluido_em is null and s.situacao in (1, 2) and s.data_entrada_fila is not null
            group by 1 order by 2 desc limit 15
            """, ct);
        tabelas.Add(new TabelaIndicadorDto("Quem ainda aguarda (fila atual), por recurso", ["", "Aguardando", "Mediana (dias)", "Maior (dias)"],
            aguard.Select(l => (IReadOnlyList<string?>)[IndicadoresSisregCalculo.Truncar((string?)l[0]), Formatar.Numero(l[1]), Formatar.Numero(l[2]), Formatar.Numero(l[3])]).ToList(), null));
        secoes.Add(new SecaoIndicadorDto("espera", "Tempo de espera",
            "Espera em fila: dias entre a entrada na fila e o agendamento. Espera até o atendimento: dias entre a entrada na fila e a data do atendimento marcado. Por recurso (o ESUS organiza a fila por recurso/especialidade).",
            false,
            [
                f.Serie("Espera até o atendimento — mediana (dias)", Mes(at, r => r.Mediana), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, destaque: true, anual: Ano(at, r => r.Mediana)),
                f.Serie("Espera até o atendimento — menor (dias)", Mes(at, r => r.Menor), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Ano(at, r => r.Menor)),
                f.Serie("Espera até o atendimento — maior (dias)", Mes(at, r => r.Maior), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Ano(at, r => r.Maior)),
                f.Serie("Espera em fila — mediana (dias)", Mes(comFila, r => r.Mediana), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, destaque: true, anual: Ano(comFila, r => r.Mediana)),
                f.Serie("Espera em fila — maior (dias)", Mes(comFila, r => r.Maior), SeloIndicador.Calculado, formato: FormatoIndicador.Dias, anual: Ano(comFila, r => r.Maior)),
            ],
            new GraficoIndicadorDto("barras", ["Espera até o atendimento — mediana (dias)", "Espera em fila — mediana (dias)"]), null, tabelas, null));

        // judicializadas: prioridade "Mandado judicial" (também grafada "Mandato")
        var jl = await sql.LinhasAsync($"""
            select s.id_esussg, s.tipo, left(s.recurso, 60), s.data_entrada_fila, s.situacao,
                   ({dataRegulacao} - s.data_entrada_fila), (s.data_agendada - s.data_entrada_fila)
            from {S} s where s.excluido_em is null and s.prioridade ilike '%judicial%' order by s.data_entrada_fila
            """, ct);
        var sitNome = new Dictionary<int, string> { [1] = "Em fila", [2] = "Pendente", [3] = "Agendada", [4] = "Saiu da fila" };
        var noPeriodo = jl.Where(l => l[3] is DateOnly d && d >= p.Inicio && d <= p.Fim).ToList();
        var jm = p.Meses.ToDictionary(m => m, m => noPeriodo.Count(l => PeriodoIndicadores.Chave((DateOnly)l[3]!) == m));
        static string Dias(object? v) => v is null || Convert.ToInt32(v, CultureInfo.InvariantCulture) < 0 ? "—" : Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        secoes.Add(new SecaoIndicadorDto("judicial", "Demandas judicializadas",
            $"Solicitações com prioridade 'Mandado judicial' no ESUS de São Gonçalo. Total no espelho: {jl.Count}; com entrada na fila no período: {noPeriodo.Count}.",
            false,
            [f.Serie("Solicitações judicializadas (pela entrada na fila)", jm, SeloIndicador.Oficial, destaque: true)],
            new GraficoIndicadorDto("barras", ["Solicitações judicializadas (pela entrada na fila)"]), null,
            jl.Count == 0 ? [] :
            [
                new TabelaIndicadorDto("Solicitações judicializadas (todas as do espelho)",
                    ["ID", "Tipo", "Recurso", "Entrada na fila", "Situação atual", "Dias até agendar", "Dias até o atendimento"],
                    jl.Select(l => (IReadOnlyList<string?>)[(string?)l[0], tipos.GetValueOrDefault(Convert.ToInt32(l[1], CultureInfo.InvariantCulture), "—"),
                        (string?)l[2], Formatar.Data(l[3]) ?? "—", sitNome.GetValueOrDefault(Convert.ToInt32(l[4], CultureInfo.InvariantCulture), "—"),
                        Dias(l[5]), Dias(l[6])]).ToList(), null),
            ],
            Tempo.Resumo(noPeriodo.Where(l => l[5] is not null && Convert.ToInt32(l[5], CultureInfo.InvariantCulture) >= 0)
                .Select(l => Convert.ToInt32(l[5], CultureInfo.InvariantCulture)))));

        var cob = (await sql.LinhasAsync($"""
            select (select count(*) from {S} where excluido_em is null),
                   (select min(iniciado_em) from smsmarica.esussg_varredura_execucao where status = 3),
                   (select max(finalizado_em) from smsmarica.esussg_varredura_execucao where status = 3)
            """, ct))[0];
        return new IndicadoresRegulacaoDto(FonteIndicadorRegulacao.EsusSg, "ESUS SG",
            "ESUS de São Gonçalo — regulação da PPI de São Gonçalo", p.Meses, agora,
            [
                $"Espelho da fila e dos agendados do município no ESUS de São Gonçalo ({Formatar.Numero(cob[0])} solicitações), primeira varredura em {Formatar.Data(cob[1]) ?? "—"}, última em {Formatar.Data(cob[2]) ?? "—"}.",
                "O ESUS não informa comparecimento, oferta de vagas nem motivo de exclusão ao município solicitante.",
            ],
            secoes);
    }

    private static async Task<Dictionary<string, Dictionary<string, int>>> PorTipoAsync(
        Sql sql, string consulta, IReadOnlyDictionary<int, string> tipos, PeriodoIndicadores p, CancellationToken ct)
    {
        var r = new Dictionary<string, Dictionary<string, int>>();
        foreach (var l in await sql.LinhasAsync(consulta, ct, p.Inicio, p.Fim))
        {
            if (l[0] is not string mes) continue;
            var nome = tipos.GetValueOrDefault(Convert.ToInt32(l[1], CultureInfo.InvariantCulture), "Outros");
            if (!r.TryGetValue(nome, out var porMes)) r[nome] = porMes = [];
            porMes[mes] = Convert.ToInt32(l[2], CultureInfo.InvariantCulture);
        }
        return r;
    }
}
