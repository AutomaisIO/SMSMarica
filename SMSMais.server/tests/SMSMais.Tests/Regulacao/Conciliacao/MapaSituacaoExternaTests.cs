using SMSMais.Core.Regulacao.Conciliacao;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Tests.Regulacao.Conciliacao;

/// <summary>
/// A tradução entre o que o sistema de regulação diz e o nosso estado (plano 05). Testes puros.
///
/// <para>O que prendem: <b>nem toda situação de lá muda o nosso estado</b> — `null` significa "não
/// mexe", e mapear tudo à força faria a varredura empurrar a solicitação de um lado para o outro a
/// cada passada; e a <b>volta é permitida</b>, porque desmarcação acontece e uma ficha que diz
/// "agendada" para quem perdeu a vaga é pior do que nenhuma ficha.</para>
/// </summary>
public class MapaSituacaoExternaTests
{
    [Theory]
    [InlineData(SituacaoSer.EmFila, StatusRegulacao.EmFilaExterna)]
    [InlineData(SituacaoSer.Pendente, StatusRegulacao.EmFilaExterna)]
    [InlineData(SituacaoSer.Agendada, StatusRegulacao.Agendada)]
    [InlineData(SituacaoSer.ChegadaConfirmada, StatusRegulacao.Agendada)]
    [InlineData(SituacaoSer.Alta, StatusRegulacao.Concluida)]
    [InlineData(SituacaoSer.Cancelada, StatusRegulacao.Cancelada)]
    public void O_SER_traduz_para_o_nosso_estado(SituacaoSer de, StatusRegulacao esperado) =>
        MapaSituacaoExterna.DeSer(de).Should().Be(esperado);

    [Theory]
    [InlineData(SituacaoSernit.EmFila, StatusRegulacao.EmFilaExterna)]
    [InlineData(SituacaoSernit.Agendada, StatusRegulacao.Agendada)]
    [InlineData(SituacaoSernit.Alta, StatusRegulacao.Concluida)]
    [InlineData(SituacaoSernit.Cancelada, StatusRegulacao.Cancelada)]
    public void O_SERNIT_traduz_para_o_nosso_estado(SituacaoSernit de, StatusRegulacao esperado) =>
        MapaSituacaoExterna.DeSernit(de).Should().Be(esperado);

    [Theory]
    [InlineData(StatusSolicitacao.Solicitada, StatusRegulacao.EmFilaExterna)]
    [InlineData(StatusSolicitacao.Agendada, StatusRegulacao.Agendada)]
    [InlineData(StatusSolicitacao.Realizada, StatusRegulacao.Concluida)]
    [InlineData(StatusSolicitacao.Cancelada, StatusRegulacao.Cancelada)]
    public void O_SISREG_traduz_para_o_nosso_estado(StatusSolicitacao de, StatusRegulacao esperado) =>
        MapaSituacaoExterna.DeSisreg(de).Should().Be(esperado);

    [Fact]
    public void A_desmarcacao_volta_o_caso_para_a_fila()
    {
        // O SER desmarca e o caso retorna à fila. Travar isso deixaria a nossa ficha dizendo
        // "agendada" para um paciente que perdeu a vaga, e ninguém descobriria pela tela.
        MapaSituacaoExterna
            .DeveAplicar(StatusRegulacao.Agendada, StatusRegulacao.EmFilaExterna)
            .Should().BeTrue();
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.Agendada, StatusRegulacao.EmFilaExterna, PapelEventoRegulacao.Sistema)
            .Should().BeTrue();
    }

    [Fact]
    public void A_mesma_situacao_lida_de_novo_nao_gera_evento()
    {
        // A varredura relê o mesmo caso todo dia; sem isto, a linha do tempo viraria um diário
        // de leituras em vez da história do caso.
        MapaSituacaoExterna
            .DeveAplicar(StatusRegulacao.Agendada, StatusRegulacao.Agendada)
            .Should().BeFalse();
    }

    [Fact]
    public void De_um_caso_encerrado_a_varredura_nao_tira_mais()
    {
        // Quem segura é a máquina de estados: de Concluída/Cancelada/Recusada não sai transição.
        foreach (var terminal in new[]
        {
            StatusRegulacao.Concluida, StatusRegulacao.Cancelada, StatusRegulacao.Recusada,
        })
        {
            MaquinaDeEstadosRegulacao.DestinosDe(terminal, PapelEventoRegulacao.Sistema)
                .Should().BeEmpty();
        }
    }
}
