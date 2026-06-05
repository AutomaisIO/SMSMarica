namespace SMSMarica.Core.Agendamentos;

/// <summary>Regra de disponibilidade recorrente, na forma usada pelo cálculo de slots.</summary>
public sealed record RegraRecorrenteSlot(
    DayOfWeek DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFim,
    DateOnly? VigenciaInicio,
    DateOnly? VigenciaFim,
    bool Ativo);

/// <summary>Janela [Inicio, Fim] em horário local (wall-clock) — avulso, bloqueio ou agendamento ocupado.</summary>
public sealed record JanelaSlot(DateTime Inicio, DateTime Fim);

/// <summary>
/// Cálculo puro (sem banco) dos horários livres de uma agenda. Slots livres =
/// (recorrências ∪ avulsos) fatiados em consultas de duração fixa, menos os que
/// se sobrepõem a bloqueios ou agendamentos ativos. Núcleo testável — ver ADR-0012.
/// </summary>
public static class CalculadoraSlots
{
    public static IReadOnlyList<JanelaSlot> Calcular(
        int duracaoMinutos,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        IEnumerable<RegraRecorrenteSlot> recorrencias,
        IEnumerable<JanelaSlot> avulsos,
        IEnumerable<JanelaSlot> bloqueios,
        IEnumerable<JanelaSlot> ocupados,
        DateOnly intervaloInicio,
        DateOnly intervaloFim)
    {
        if (duracaoMinutos <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duracaoMinutos), "Duração deve ser positiva.");
        }

        var duracao = TimeSpan.FromMinutes(duracaoMinutos);
        var bloqueiosList = bloqueios as IReadOnlyCollection<JanelaSlot> ?? [.. bloqueios];
        var ocupadosList = ocupados as IReadOnlyCollection<JanelaSlot> ?? [.. ocupados];

        // Limita a janela efetiva à interseção do intervalo pedido com a vigência da agenda.
        var inicioEfetivo = Maior(intervaloInicio, vigenciaInicio);
        var fimEfetivo = vigenciaFim is { } vf ? Menor(intervaloFim, vf) : intervaloFim;
        if (inicioEfetivo > fimEfetivo)
        {
            return [];
        }

        var janelas = new List<JanelaSlot>();
        ColetarJanelasRecorrentes(recorrencias, inicioEfetivo, fimEfetivo, janelas);
        ColetarJanelasAvulsas(avulsos, inicioEfetivo, fimEfetivo, janelas);

        var slots = new SortedSet<JanelaSlot>(Comparer<JanelaSlot>.Create(
            (a, b) => a.Inicio != b.Inicio ? a.Inicio.CompareTo(b.Inicio) : a.Fim.CompareTo(b.Fim)));

        foreach (var janela in janelas)
        {
            for (var t = janela.Inicio; t + duracao <= janela.Fim; t += duracao)
            {
                var slot = new JanelaSlot(t, t + duracao);
                if (!Sobrepoe(slot, bloqueiosList) && !Sobrepoe(slot, ocupadosList))
                {
                    slots.Add(slot);
                }
            }
        }

        return [.. slots];
    }

    private static void ColetarJanelasRecorrentes(
        IEnumerable<RegraRecorrenteSlot> recorrencias,
        DateOnly inicio,
        DateOnly fim,
        List<JanelaSlot> destino)
    {
        foreach (var regra in recorrencias)
        {
            if (!regra.Ativo || regra.HoraFim <= regra.HoraInicio)
            {
                continue;
            }

            var de = regra.VigenciaInicio is { } vi ? Maior(inicio, vi) : inicio;
            var ate = regra.VigenciaFim is { } vf ? Menor(fim, vf) : fim;

            for (var dia = de; dia <= ate; dia = dia.AddDays(1))
            {
                if (dia.DayOfWeek == regra.DiaSemana)
                {
                    destino.Add(new JanelaSlot(dia.ToDateTime(regra.HoraInicio), dia.ToDateTime(regra.HoraFim)));
                }
            }
        }
    }

    private static void ColetarJanelasAvulsas(
        IEnumerable<JanelaSlot> avulsos,
        DateOnly inicio,
        DateOnly fim,
        List<JanelaSlot> destino)
    {
        foreach (var avulso in avulsos)
        {
            if (avulso.Fim <= avulso.Inicio)
            {
                continue;
            }

            // Inclui o avulso se sua data se sobrepõe ao intervalo efetivo (vigência já recortada).
            if (DateOnly.FromDateTime(avulso.Fim) >= inicio && DateOnly.FromDateTime(avulso.Inicio) <= fim)
            {
                destino.Add(avulso);
            }
        }
    }

    private static bool Sobrepoe(JanelaSlot slot, IEnumerable<JanelaSlot> janelas)
    {
        foreach (var j in janelas)
        {
            // Sobreposição de intervalos abertos: começa antes do outro acabar e o outro começa antes deste acabar.
            if (slot.Inicio < j.Fim && j.Inicio < slot.Fim)
            {
                return true;
            }
        }

        return false;
    }

    private static DateOnly Maior(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Menor(DateOnly a, DateOnly b) => a < b ? a : b;
}
