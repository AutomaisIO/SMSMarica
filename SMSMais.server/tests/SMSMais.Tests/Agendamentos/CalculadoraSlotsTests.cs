using SMSMais.Core.Agendamentos;

namespace SMSMais.Tests.Agendamentos;

/// <summary>
/// Testes puros (sem banco) do núcleo de cálculo de horários livres — ADR-0012.
/// Datas-âncora: 2026-06-08 e 2026-06-15 são segundas-feiras; 2026-06-09 é terça.
/// </summary>
public class CalculadoraSlotsTests
{
    private static readonly DateOnly Segunda = new(2026, 6, 8);
    private static readonly DateOnly SegundaSeguinte = new(2026, 6, 15);
    private static readonly DateOnly Terca = new(2026, 6, 9);

    private static RegraRecorrenteSlot Recorrencia(
        DayOfWeek dia, int hInicio, int hFim, DateOnly? vigInicio = null, DateOnly? vigFim = null, bool ativo = true) =>
        new(dia, new TimeOnly(hInicio, 0), new TimeOnly(hFim, 0), vigInicio, vigFim, ativo);

    private static JanelaSlot Janela(DateOnly dia, int hInicio, int mInicio, int hFim, int mFim) =>
        new(dia.ToDateTime(new TimeOnly(hInicio, mInicio)), dia.ToDateTime(new TimeOnly(hFim, mFim)));

    [Fact]
    public void Recorrencia_gera_slots_fatiados_pela_duracao()
    {
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 30,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 10)],
            avulsos: [], bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: Segunda);

        slots.Should().HaveCount(4);
        slots[0].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(8, 0)));
        slots[0].Fim.Should().Be(Segunda.ToDateTime(new TimeOnly(8, 30)));
        slots[^1].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(9, 30)));
    }

    [Fact]
    public void Recorrencia_so_gera_no_dia_da_semana_correto()
    {
        // Intervalo de uma semana, mas a regra é só segunda → só os dois mondays do range.
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 60,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 10)],
            avulsos: [], bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: SegundaSeguinte);

        slots.Should().HaveCount(4); // 2 slots × 2 segundas
        slots.Select(s => DateOnly.FromDateTime(s.Inicio)).Distinct()
            .Should().BeEquivalentTo([Segunda, SegundaSeguinte]);
    }

    [Fact]
    public void Bloqueio_remove_apenas_os_slots_que_se_sobrepoem()
    {
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 30,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 10)],
            avulsos: [],
            bloqueios: [Janela(Segunda, 8, 30, 9, 0)],
            ocupados: [],
            intervaloInicio: Segunda, intervaloFim: Segunda);

        // Remove só o slot 08:30–09:00; 08:00 e 09:00 e 09:30 permanecem.
        slots.Should().HaveCount(3);
        slots.Select(s => s.Inicio.TimeOfDay).Should().NotContain(new TimeOnly(8, 30).ToTimeSpan());
    }

    [Fact]
    public void Avulso_adiciona_slots_fora_da_recorrencia()
    {
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 30,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [], // sem recorrência
            avulsos: [Janela(Terca, 14, 0, 15, 0)],
            bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: SegundaSeguinte);

        slots.Should().HaveCount(2);
        slots[0].Inicio.Should().Be(Terca.ToDateTime(new TimeOnly(14, 0)));
        slots[1].Inicio.Should().Be(Terca.ToDateTime(new TimeOnly(14, 30)));
    }

    [Fact]
    public void Agendamento_ocupado_remove_o_slot()
    {
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 30,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 10)],
            avulsos: [], bloqueios: [],
            ocupados: [Janela(Segunda, 9, 0, 9, 30)],
            intervaloInicio: Segunda, intervaloFim: Segunda);

        slots.Should().HaveCount(3);
        slots.Select(s => s.Inicio.TimeOfDay).Should().NotContain(new TimeOnly(9, 0).ToTimeSpan());
    }

    [Fact]
    public void Vigencia_da_agenda_recorta_o_intervalo()
    {
        // Agenda só vale a partir da próxima segunda; consulta cai antes → vazio.
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 30,
            vigenciaInicio: SegundaSeguinte, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 10)],
            avulsos: [], bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: Segunda);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void Vigencia_da_recorrencia_limita_as_datas_geradas()
    {
        // Regra com vigência só até a primeira segunda → não gera na segunda seguinte.
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 60,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 10, vigFim: Segunda)],
            avulsos: [], bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: SegundaSeguinte);

        slots.Select(s => DateOnly.FromDateTime(s.Inicio)).Distinct().Should().BeEquivalentTo([Segunda]);
    }

    [Fact]
    public void Duracao_que_nao_divide_a_janela_descarta_a_sobra()
    {
        // 08:00–09:00 com slots de 25min → 08:00 e 08:25; 08:50+25 ultrapassa 09:00.
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 25,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 9)],
            avulsos: [], bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: Segunda);

        slots.Should().HaveCount(2);
        slots[0].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(8, 0)));
        slots[1].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(8, 25)));
    }

    [Fact]
    public void Recorrencia_e_avulso_no_mesmo_horario_nao_duplicam_o_slot()
    {
        var slots = CalculadoraSlots.Calcular(
            duracaoMinutos: 60,
            vigenciaInicio: Segunda, vigenciaFim: null,
            recorrencias: [Recorrencia(DayOfWeek.Monday, 8, 9)],
            avulsos: [Janela(Segunda, 8, 0, 9, 0)],
            bloqueios: [], ocupados: [],
            intervaloInicio: Segunda, intervaloFim: Segunda);

        slots.Should().HaveCount(1);
        slots[0].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(8, 0)));
    }
}
