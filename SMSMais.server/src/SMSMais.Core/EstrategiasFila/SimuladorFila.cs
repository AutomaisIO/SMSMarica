using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// A projeção da fila: função <b>pura</b>, determinística, sem banco e sem modelo de linguagem.
///
/// <para><b>Por que é uma função e não parte do prompt.</b> Modelo de linguagem erra aritmética e
/// não é reproduzível; um gestor não pode levar ao secretário um "zera em 14 semanas" que muda
/// quando se pergunta de novo. Aqui o número sai de uma conta que qualquer pessoa confere no
/// papel, e o agente só escolhe os parâmetros e chama isto (ADR-0058).</para>
///
/// <para><b>O modelo.</b> Passo semanal. A cada semana entram <c>entrada</c> pessoas; a capacidade
/// efetiva é <c>profissionais × turnos/semana × atendimentos/turno × aproveitamento</c> mais os
/// mutirões daquela semana; atende-se o mínimo entre a capacidade e quem está esperando (fila
/// anterior + quem chegou). Não há sazonalidade nem crescimento da demanda: a entrada é constante,
/// e quem quiser "a demanda cresce 10%" trava <c>EntradaSemanal</c> num valor maior.</para>
///
/// <para><b>Convenção de tempo:</b> semana 0 é o retrato de hoje; semana 1 é a primeira semana
/// simulada. "Zera na semana N" significa que ao fim da semana N a fila é zero.</para>
/// </summary>
public static class SimuladorFila
{
    public static ProjecaoDto Projetar(ParametrosEstrategia p, int filaInicial)
    {
        var horizonte = Math.Clamp(p.HorizonteSemanas <= 0 ? ParametrosEstrategia.HorizontePadrao : p.HorizonteSemanas,
            1, ParametrosEstrategia.HorizonteMaximo);
        var capacidade = p.CapacidadeSemanal();
        var entrada = Math.Max(0, p.EntradaSemanal.Valor);
        var fila0 = Math.Max(0, filaInicial);

        var mutiroes = (p.Mutiroes ?? [])
            .Where(m => m.Semana >= 1 && m.Vagas > 0)
            .GroupBy(m => m.Semana)
            .ToDictionary(g => g.Key, g => (double)g.Sum(m => m.Vagas));

        var serie = new List<PontoProjecaoDto>(horizonte + 1)
        {
            new(0, fila0, capacidade, 0, 0),
        };

        double fila = fila0;
        double atendidosAteZerar = 0;
        double pico = fila0;
        int? semanaZera = null;

        for (var semana = 1; semana <= horizonte; semana++)
        {
            var capSemana = capacidade + (mutiroes.TryGetValue(semana, out var extra) ? extra : 0);
            var esperando = fila + entrada;
            var atendidos = Math.Min(capSemana, esperando);
            fila = esperando - atendidos;
            if (fila < 1e-9) fila = 0;

            if (semanaZera is null) atendidosAteZerar += atendidos;
            if (fila > pico) pico = fila;

            serie.Add(new PontoProjecaoDto(semana, Arredondar(fila), Arredondar(capSemana), Arredondar(atendidos), entrada));

            // Zerar significa que a fila do fim da semana é zero E que havia alguém para atender —
            // uma fila que já começa vazia e continua vazia "zera" na semana 1 por convenção.
            if (semanaZera is null && fila == 0) semanaZera = semana;
        }

        var zera = semanaZera is not null;
        var crescimento = zera ? 0 : Math.Max(0, entrada - capacidade);

        // Para zerar no prazo P, precisa atender fila0 + P×entrada em P semanas — mutirões já
        // dentro do prazo abatem a conta.
        double? paraZerarNoPrazo = null;
        if (p.PrazoAlvoSemanas is { } prazo && prazo > 0)
        {
            var mutiroesNoPrazo = mutiroes.Where(m => m.Key <= prazo).Sum(m => m.Value);
            paraZerarNoPrazo = Math.Max(0, (fila0 + prazo * entrada - mutiroesNoPrazo) / prazo);
        }

        return new ProjecaoDto(
            FilaInicial: filaInicial,
            CapacidadeSemanal: Arredondar(capacidade),
            VagasSemanais: Arredondar(p.VagasSemanais()),
            EntradaSemanal: entrada,
            Zera: zera,
            SemanaZera: semanaZera,
            FilaFinal: Arredondar(fila),
            CrescimentoSemanal: Arredondar(crescimento),
            CapacidadeEquilibrio: entrada,
            CapacidadeParaZerarNoPrazo: paraZerarNoPrazo is { } v ? Arredondar(v) : null,
            AtendidosAteZerar: Arredondar(atendidosAteZerar),
            PicoFila: Arredondar(pico),
            HorizonteSemanas: horizonte,
            Serie: serie);
    }

    /// <summary>
    /// Quantos profissionais (mantendo turnos, atendimentos/turno e aproveitamento) fariam a
    /// capacidade chegar a <paramref name="capacidadeAlvo"/>. Serve para a tela sugerir "faltam
    /// 2,3 profissionais" sem chamar o agente.
    /// </summary>
    public static double? ProfissionaisPara(ParametrosEstrategia p, double capacidadeAlvo)
    {
        var porProfissional = Math.Max(0, p.TurnosPorProfissionalSemana.Valor)
            * Math.Max(0, p.AtendimentosPorTurno.Valor) * Math.Clamp(p.Aproveitamento.Valor, 0, 1);
        if (porProfissional <= 0) return null;
        return Arredondar(capacidadeAlvo / porProfissional);
    }

    private static double Arredondar(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
