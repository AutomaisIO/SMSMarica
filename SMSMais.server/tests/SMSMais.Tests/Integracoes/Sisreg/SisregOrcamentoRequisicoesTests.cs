using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// O contador rolante que todos os motores do SISREG dividem. Ele existe porque cada motor tinha o
/// próprio teto e nenhum enxergava o gasto do outro — e o CAPTCHA aparecia sem que nenhum deles
/// tivesse "estourado" nada.
/// </summary>
public class SisregOrcamentoRequisicoesTests
{
    [Fact]
    public void Conta_o_que_foi_gasto_na_janela()
    {
        var orcamento = new SisregOrcamentoRequisicoes();

        for (var i = 0; i < 7; i++) orcamento.Registrar();

        orcamento.GastasNaUltimaHora().Should().Be(7);
    }

    [Fact]
    public void Restante_nunca_fica_negativo()
    {
        var orcamento = new SisregOrcamentoRequisicoes();

        for (var i = 0; i < 10; i++) orcamento.Registrar();

        // Estourar o teto não pode virar "sobra negativa" e destravar comparações do tipo
        // `custo <= restante` por acidente aritmético.
        orcamento.Restante(4).Should().Be(0);
        orcamento.Restante(25).Should().Be(15);
    }

    [Fact]
    public void Sem_gasto_nao_ha_espera()
    {
        new SisregOrcamentoRequisicoes().EsperaAteLiberar().Should().BeNull();
    }

    [Fact]
    public void Com_gasto_a_espera_cabe_na_janela_de_uma_hora()
    {
        var orcamento = new SisregOrcamentoRequisicoes();
        orcamento.Registrar();

        var espera = orcamento.EsperaAteLiberar();

        espera.Should().NotBeNull();
        espera!.Value.Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromHours(1));
    }

    /// <summary>
    /// A reserva do operador não é decoração: é ela que garante que o lote noturno não consuma o
    /// orçamento inteiro e o operador chegue de manhã com o SISREG bloqueado sem ter clicado nada.
    /// </summary>
    [Fact]
    public void Teto_automatico_desconta_a_reserva_do_operador()
    {
        var opcoes = new SisregOrcamentoOpcoes { TetoPorHora = 500, ReservaOperador = 100 };

        opcoes.TetoAutomatico.Should().Be(400);
    }

    [Fact]
    public void Teto_automatico_nunca_zera_mesmo_com_reserva_absurda()
    {
        var opcoes = new SisregOrcamentoOpcoes { TetoPorHora = 50, ReservaOperador = 500 };

        // Zero aqui travaria o motor para sempre, em silêncio.
        opcoes.TetoAutomatico.Should().BeGreaterThan(0);
    }
}
