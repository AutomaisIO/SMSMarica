using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Distribuição dos horários diários das unidades.
///
/// <para>O que está sendo protegido: horário caído dentro da faixa em que o SISREG bloqueia a
/// exportação da agenda (08:00–15:00, com corte de entrada às 07:30) é uma varredura que
/// <b>simplesmente não roda</b> — e não roda em silêncio, todo dia, até alguém reparar que aquela
/// unidade parou de importar.</para>
/// </summary>
public class DistribuicaoHorariosRedeTests
{
    private static readonly TimeOnly CorteEntrada = new(7, 30);
    private static readonly TimeOnly BloqueioFim = new(15, 0);

    private static bool NaFaixaBloqueada(TimeOnly t) => t > CorteEntrada && t < BloqueioFim;

    [Fact]
    public void Comeca_na_hora_pedida_e_respeita_o_intervalo()
    {
        var horarios = SisregMapeamentoLoteService.DistribuirHorarios(new TimeOnly(15, 0), 20, 4);

        horarios.Should().Equal(
            new TimeOnly(15, 0), new TimeOnly(15, 20), new TimeOnly(15, 40), new TimeOnly(16, 0));
    }

    [Fact]
    public void Nenhum_horario_cai_na_faixa_bloqueada()
    {
        // 42 unidades a 20 min é o caso real de Maricá: passa da meia-noite e chega perto das 07:30.
        var horarios = SisregMapeamentoLoteService.DistribuirHorarios(new TimeOnly(15, 0), 20, 42);

        horarios.Should().HaveCount(42);
        horarios.Should().NotContain(t => NaFaixaBloqueada(t));
    }

    [Fact]
    public void Pula_a_faixa_bloqueada_em_vez_de_parar_nela()
    {
        // Começando às 07:00, o segundo horário cairia às 07:30 e o terceiro dentro do bloqueio.
        var horarios = SisregMapeamentoLoteService.DistribuirHorarios(new TimeOnly(7, 0), 30, 3);

        horarios.Should().HaveCount(3);
        horarios.Should().NotContain(t => NaFaixaBloqueada(t));
        horarios[0].Should().Be(new TimeOnly(7, 0));
        horarios[1].Should().Be(new TimeOnly(7, 30)); // 07:30 ainda entra — é o corte, não o bloqueio
        horarios[2].Should().Be(new TimeOnly(15, 0)); // pulou 08:00–14:30
    }

    /// <summary>
    /// A rede é maior que a janela: 42 unidades a 30 min não cabem entre 15:00 e 07:30 sem repetir
    /// horário. Repetir não é erro (duas unidades no mesmo minuto competem pela sessão, mas rodam),
    /// e o método precisa devolver a quantidade pedida em vez de entregar menos em silêncio.
    /// </summary>
    [Fact]
    public void Devolve_sempre_a_quantidade_pedida_mesmo_quando_a_janela_aperta()
    {
        var horarios = SisregMapeamentoLoteService.DistribuirHorarios(new TimeOnly(15, 0), 30, 42);

        horarios.Should().HaveCount(42);
        horarios.Should().NotContain(t => NaFaixaBloqueada(t));
    }

    [Fact]
    public void Zero_unidades_nao_quebra()
    {
        SisregMapeamentoLoteService.DistribuirHorarios(new TimeOnly(15, 0), 20, 0).Should().BeEmpty();
    }
}
