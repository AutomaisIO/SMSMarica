using System.Globalization;
using System.Text;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// O briefing do agente: papel, regras (trava é lei; números vêm da ferramenta) e o cenário em
/// markdown. Vai inteiro no bloco <c>system</c> com cache — é idêntico entre as iterações da mesma
/// rodada.
/// </summary>
public static class EstrategiaPrompt
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Dias = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    public static string Montar(CenarioFilaDto c, ParametrosEstrategia p)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""
            Você é o planejador de oferta da Secretaria de Saúde. Sua tarefa: propor uma ESTRATÉGIA para
            reduzir ou zerar a fila de espera de UM procedimento regulado, mexendo na oferta (profissionais,
            dias, horas, atendimentos por hora, mutirões).

            REGRAS — leia com atenção:
            1. Você NÃO faz conta. Toda projeção sai da ferramenta `simular`, que roda o nosso modelo
               determinístico. Chame-a quantas vezes precisar para testar combinações. Nunca afirme um
               prazo que não veio dela.
            2. Parâmetro TRAVADO é fato: não pode mudar. Se mandar valor diferente, a ferramenta devolve
               erro e você corrige. Parâmetro LIVRE pode mudar dentro do mínimo/máximo indicado.
            3. Procure a estratégia MAIS ECONÔMICA que cumpra o objetivo: o menor acréscimo de recursos
               (profissionais, dias, horas) que atinja o prazo. Não proponha o dobro de tudo se 20% a mais
               resolve. Prefira aumentar horas/dias de quem já atende a contratar; prefira mutirão pontual
               para a "corcova" da fila antiga e capacidade permanente para o fluxo.
            4. O `aproveitamento` mede o quanto da vaga ofertada vira atendimento de verdade. Se está baixo,
               a primeira ação costuma ser fazer a vaga existente ser usada (confirmação, remarcação,
               absenteísmo), não abrir vaga nova — e isso vale como ação na proposta.
            5. Termine SEMPRE chamando `propor_estrategia`, uma única vez, com os parâmetros finais, um
               resumo em português claro para um gestor (3 a 6 frases), as ações concretas (o que fazer,
               onde, quanto de vaga por semana isso rende) e os riscos. Sem essa chamada a rodada é
               descartada.
            6. Escreva em português do Brasil, sem jargão técnico. Nomeie unidades e profissionais como
               aparecem no cenário.
            """);

        sb.AppendLine();
        sb.AppendLine("# Cenário atual");
        sb.AppendLine();
        sb.AppendLine($"**Procedimento:** {c.Procedimento.Nome}"
            + (c.Procedimento.Codigo is { } cod ? $" (código SISREG {cod}{(c.Procedimento.EhGrupo ? ", GRUPO" : "")})" : " (sem escala cadastrada no SISREG)")
            + (c.Procedimento.NomeCanonico is { } can ? $" — catálogo: {can}" : ""));
        if (c.Procedimento.Familia.Count > 1)
            sb.AppendLine($"Família considerada na fila: {string.Join("; ", c.Procedimento.Familia)}");
        sb.AppendLine();

        sb.AppendLine("## Fila hoje");
        sb.AppendLine($"- Pessoas esperando: **{N(c.Fila.Total)}**");
        if (c.Fila.EsperaMedianaDias is { } med)
            sb.AppendLine($"- Espera: mediana {med} dias, p90 {c.Fila.EsperaP90Dias} dias, máxima {c.Fila.EsperaMaxDias} dias");
        if (c.Fila.PorRisco.Count > 0)
            sb.AppendLine("- Por risco (0=vermelho … 3=azul): " + string.Join(", ", c.Fila.PorRisco.OrderBy(k => k.Key).Select(k => $"{k.Key}: {N(k.Value)}")));
        sb.AppendLine("- Faixas: " + string.Join(", ", c.Fila.Faixas.Where(f => f.Volume > 0).Select(f => $"{f.Rotulo} {N(f.Volume)}")));
        if (c.SaidaSemAgendarFracao is { } ssa)
            sb.AppendLine($"- De quem saiu da fila desde a carga, {ssa:P0} saiu SEM agendar (cancelou/negado)");
        sb.AppendLine();

        sb.AppendLine("## Ritmo (semanas cheias)");
        sb.AppendLine($"- Entrada: **{D(c.Entrada.MediaSemanal12)}/semana** (média 12 sem.); 26 sem.: {D(c.Entrada.MediaSemanal26)}"
            + (c.Entrada.Tendencia is { } t ? $"; tendência (4 últimas ÷ 12): {D(t)}" : ""));
        sb.AppendLine($"- Vazão real (marcações): **{D(c.Vazao.MediaSemanal12)}/semana** (média 12 sem.); 26 sem.: {D(c.Vazao.MediaSemanal26)}");
        sb.AppendLine("- Série de entrada (últimas 12): " + string.Join(" ", c.Entrada.Serie.TakeLast(12).Select(s => s.Quantidade)));
        sb.AppendLine("- Série de vazão (últimas 12): " + string.Join(" ", c.Vazao.Serie.TakeLast(12).Select(s => s.Quantidade)));
        sb.AppendLine();

        sb.AppendLine("## Oferta vigente (regulada; agenda local fora)");
        var o = c.Oferta;
        sb.AppendLine($"- Vagas de regulação por semana: **{N(o.VagasRegulacaoSemana)}** (1ª vez {N(o.VagasPrimeiraVezSemana)} + reserva {N(o.VagasReservaSemana)}); retorno {N(o.VagasRetornoSemana)}; total {N(o.VagasTotalSemana)}"
            + (o.VagasAgendaLocalSemana > 0 ? $"; agenda local (fora da conta) {N(o.VagasAgendaLocalSemana)}" : ""));
        sb.AppendLine($"- Profissionais: {o.Profissionais.Count}; unidades reguladas: {o.Unidades.Count(u => !u.AgendaLocal)}; blocos/semana: {N(o.Blocos)}");
        sb.AppendLine($"- Dias com oferta: {string.Join(", ", o.DiasSemana.Select(d => Dias[d]))}"
            + (o.HoraInicioTipica is { } hi ? $"; janela {hi:HH\\:mm}–{o.HoraFimTipica:HH\\:mm}" : ""));
        sb.AppendLine($"- Por profissional: {D(o.MediaDiasPorProfissional)} dias/semana, {D(o.MediaHorasPorProfissionalDia)} h/dia, {D(o.AtendimentosPorHoraBase)} atendimentos/hora");
        sb.AppendLine($"- Aproveitamento medido ({c.Ocupacao.SemanasMedidas} sem.): "
            + (c.Ocupacao.Aproveitamento is { } ap ? $"**{ap:P0}** ({N(c.Ocupacao.Agendados)} marcações em {N(c.Ocupacao.VagasRegulacaoOfertadas)} vagas)" : "sem oferta no período (não medido)"));
        sb.AppendLine();

        if (o.Unidades.Count > 0)
        {
            sb.AppendLine("| Unidade | Agenda | Profissionais | Vagas reg./sem | Total/sem |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (var u in o.Unidades)
                sb.AppendLine($"| {u.Nome} | {(u.AgendaLocal ? "local" : "regulada")} | {u.Profissionais} | {u.VagasRegulacaoSemana} | {u.VagasTotalSemana} |");
            sb.AppendLine();
        }

        if (o.Profissionais.Count > 0)
        {
            sb.AppendLine("| Profissional | CBO | Unidade | Dias | Vagas reg./sem |");
            sb.AppendLine("|---|---|---|---|---:|");
            foreach (var pr in o.Profissionais.Take(40))
                sb.AppendLine($"| {pr.Nome} | {pr.Cbo ?? "-"} | {pr.Unidade} | {string.Join(",", pr.Dias.Select(d => Dias[d]))} | {pr.VagasRegulacaoSemana} |");
            if (o.Profissionais.Count > 40) sb.AppendLine($"| … mais {o.Profissionais.Count - 40} | | | | |");
            sb.AppendLine();
        }

        sb.AppendLine("## Parâmetros da simulação");
        sb.AppendLine($"- Objetivo: **{p.Objetivo}**" + (p.PrazoAlvoSemanas is { } pz ? $" — prazo alvo: {pz} semanas" : ""));
        sb.AppendLine("- Capacidade/semana = profissionais × diasPorSemana × horasPorDia × atendimentosPorHora × aproveitamento (+ mutirões)");
        sb.AppendLine();
        sb.AppendLine("| Parâmetro | Valor atual | Estado | Mín | Máx |");
        sb.AppendLine("|---|---:|---|---:|---:|");
        foreach (var (nome, par) in p.Numericos())
            sb.AppendLine($"| {Camel(nome)} | {D(par.Valor)} | {(par.Travado ? "TRAVADO" : "livre")} | {(par.Min is { } mn ? D(mn) : "-")} | {(par.Max is { } mx ? D(mx) : "-")} |");
        sb.AppendLine($"| mutiroes | {(p.Mutiroes.Count == 0 ? "nenhum" : string.Join("; ", p.Mutiroes.Select(m => $"sem {m.Semana}: +{m.Vagas}")))} | {(p.MutiroesTravados ? "TRAVADO" : "livre")} | | |");
        sb.AppendLine();
        sb.AppendLine($"Horizonte da simulação: {p.HorizonteSemanas} semanas. Semana 1 é a próxima semana.");

        return sb.ToString();
    }

    private static string N(int v) => v.ToString("N0", Pt);
    private static string D(double v) => v.ToString("0.##", Pt);
    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];
}
