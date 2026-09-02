using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// O contrato do <c>ProximoRunEm</c> — a peça mais silenciosa da varredura.
///
/// <para><b>Nulo significa "nunca dispara"</b>, não "dispara já". É deliberado: se nulo fosse
/// elegível, o primeiro tick depois de um deploy varreria no meio da tarde e derrubaria a sessão de
/// quem está atendendo. O preço dessa escolha é que <b>todo caminho que liga ou reprograma uma
/// agenda precisa calcular o próximo disparo</b> — esquecer não dá erro, não aparece na tela e não
/// entra em log: a unidade fica ativa, com horário bonito, e simplesmente nunca roda.</para>
///
/// <para>Aconteceu em 01/09/2026: as 45 unidades foram programadas com <c>ProximoRunEm</c> nulo e
/// nenhuma importou agenda. Estes testes existem para que a próxima vez falhe aqui.</para>
/// </summary>
public class ProximoRunEmAgendaTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly TimeOnly CorteEntrada = new(7, 30);
    private static readonly TimeOnly BloqueioFim = new(15, 0);

    /// <summary>01/09/2026 22:00 em Brasília = 02/09 01:00 UTC. Fora do bloqueio.</summary>
    private static readonly DateTime AgoraUtc = new(2026, 9, 2, 1, 0, 0, DateTimeKind.Utc);

    private static SisregVarreduraAgenda Agenda(TimeOnly hora, DateTime? proximoRunEm) =>
        new()
        {
            UnidadeId = Guid.NewGuid(),
            Ativo = true,
            HoraLocal = hora,
            DiasAFrente = 21,
            ProximoRunEm = proximoRunEm,
        };

    private static DecisaoVarredura Decidir(SisregVarreduraAgenda agenda, TimeOnly horaLocalAgora) =>
        DecididorVarreduraSisreg.Decidir(
            agenda, AgoraUtc, horaLocalAgora, CorteEntrada, BloqueioFim,
            varreduraViva: false, importacaoViva: false);

    /// <summary>
    /// O sintoma exato do incidente: agenda ativa, horário definido, e nada acontece. Se algum dia
    /// alguém "simplificar" tratando nulo como elegível, é aqui que a intenção fica registrada.
    /// </summary>
    [Fact]
    public void Agenda_ativa_com_proximo_run_nulo_nunca_dispara()
    {
        var orfa = Agenda(new TimeOnly(18, 0), proximoRunEm: null);

        Decidir(orfa, new TimeOnly(18, 0)).Should().Be(DecisaoVarredura.Aguardar);
        // Nem no horário exato, nem depois: sem o próximo disparo calculado, não existe gatilho.
        Decidir(orfa, new TimeOnly(23, 59)).Should().Be(DecisaoVarredura.Aguardar);
    }

    [Fact]
    public void Com_o_proximo_run_calculado_a_agenda_dispara()
    {
        var pronta = Agenda(new TimeOnly(18, 0), proximoRunEm: AgoraUtc.AddMinutes(-5));

        Decidir(pronta, new TimeOnly(22, 0)).Should().Be(DecisaoVarredura.Disparar);
    }

    /// <summary>
    /// É o cálculo que "Programar todas as unidades" e "Habilitar todas" precisam fazer. Sempre no
    /// futuro: devolver um horário já passado faria a unidade disparar no mesmo instante em que foi
    /// programada — no meio da tarde, e não no horário escolhido.
    /// </summary>
    [Theory]
    [InlineData(18, 0)]
    [InlineData(0, 10)]
    [InlineData(23, 50)]
    public void Proximo_diario_cai_sempre_no_futuro_e_no_horario_escolhido(int hora, int minuto)
    {
        var horaLocal = new TimeOnly(hora, minuto);

        var proximo = DecididorVarreduraSisreg.ProximoDiario(horaLocal, AgoraUtc, Brasilia);

        proximo.Should().BeAfter(AgoraUtc);
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(proximo, Brasilia))
            .Should().Be(horaLocal);
        // E dentro de 24h: mais que isso significaria pular um dia inteiro de agenda.
        proximo.Should().BeBefore(AgoraUtc.AddDays(1).AddMinutes(1));
    }

    /// <summary>
    /// A distribuição escalonada só funciona se cada unidade guardar o SEU horário. Um cálculo que
    /// ignorasse a hora da agenda amontoaria as 45 no mesmo minuto — e elas competiriam pela mesma
    /// sessão do SISREG a noite toda.
    /// </summary>
    [Fact]
    public void Horarios_diferentes_produzem_proximos_disparos_diferentes()
    {
        var primeira = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(18, 0), AgoraUtc, Brasilia);
        var segunda = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(18, 10), AgoraUtc, Brasilia);

        (segunda - primeira).Should().Be(TimeSpan.FromMinutes(10));
    }
}
