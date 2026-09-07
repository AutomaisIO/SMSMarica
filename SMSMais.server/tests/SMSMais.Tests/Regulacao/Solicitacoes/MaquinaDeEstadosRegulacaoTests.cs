using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Regulacao.Solicitacoes;

/// <summary>
/// A tabela de transições (plano 04). Testes puros, sem banco: a regra destilada é o que impede
/// que cada endpoint novo reinvente um pedaço dela.
///
/// <para>O que prendem, e por quê: a ponta <b>não</b> cancela o que o agente já assumiu (senão o
/// trabalho de quem está no caso some debaixo dele); o agente <b>não</b> devolve o que ainda não
/// assumiu; e o caso <b>não</b> volta de um estado terminal — uma solicitação recusada que
/// reabrisse sozinha seria indistinguível de uma nova.</para>
/// </summary>
public class MaquinaDeEstadosRegulacaoTests
{
    [Theory]
    [InlineData(StatusRegulacao.Rascunho, StatusRegulacao.PendenteRegulacao, PapelEventoRegulacao.Solicitante)]
    [InlineData(StatusRegulacao.Devolvida, StatusRegulacao.PendenteRegulacao, PapelEventoRegulacao.Solicitante)]
    [InlineData(StatusRegulacao.Rascunho, StatusRegulacao.Cancelada, PapelEventoRegulacao.Solicitante)]
    [InlineData(StatusRegulacao.PendenteRegulacao, StatusRegulacao.EmAnalise, PapelEventoRegulacao.Agente)]
    [InlineData(StatusRegulacao.EmAnalise, StatusRegulacao.Devolvida, PapelEventoRegulacao.Agente)]
    [InlineData(StatusRegulacao.EmAnalise, StatusRegulacao.EnviadaAoSistema, PapelEventoRegulacao.Agente)]
    [InlineData(StatusRegulacao.EnviandoAoSistema, StatusRegulacao.FalhaEnvio, PapelEventoRegulacao.Sistema)]
    [InlineData(StatusRegulacao.EnviadaAoSistema, StatusRegulacao.EmFilaExterna, PapelEventoRegulacao.Sistema)]
    public void Transicoes_do_caminho_feliz_sao_permitidas(
        StatusRegulacao de, StatusRegulacao para, PapelEventoRegulacao papel)
    {
        MaquinaDeEstadosRegulacao.PodeTransitar(de, para, papel).Should().BeTrue();
        MaquinaDeEstadosRegulacao.EventoDe(de, para, papel).Should().NotBeNull();
    }

    [Fact]
    public void Ponta_nao_cancela_o_que_o_agente_ja_assumiu()
    {
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.EmAnalise, StatusRegulacao.Cancelada, PapelEventoRegulacao.Solicitante)
            .Should().BeFalse("depois de assumido, cancelar é decisão do agente");

        // E o agente tem a saída dele para o mesmo caso: recusar, com motivo.
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.EmAnalise, StatusRegulacao.Recusada, PapelEventoRegulacao.Agente)
            .Should().BeTrue();
    }

    [Fact]
    public void Agente_nao_devolve_o_que_nao_assumiu()
    {
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.PendenteRegulacao, StatusRegulacao.Devolvida, PapelEventoRegulacao.Agente)
            .Should().BeFalse("devolver sem assumir deixaria o caso sem responsável na trilha");
    }

    [Fact]
    public void Papel_errado_na_transicao_certa_e_recusado()
    {
        // A transição existe — mas é do agente. Um solicitante que a alcançasse assumiria o
        // próprio caso e furaria a triagem inteira.
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.PendenteRegulacao, StatusRegulacao.EmAnalise, PapelEventoRegulacao.Solicitante)
            .Should().BeFalse();
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.PendenteRegulacao, StatusRegulacao.EmAnalise, PapelEventoRegulacao.Agente)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusRegulacao.Concluida)]
    [InlineData(StatusRegulacao.Cancelada)]
    [InlineData(StatusRegulacao.Recusada)]
    public void De_estado_terminal_ninguem_sai(StatusRegulacao terminal)
    {
        MaquinaDeEstadosRegulacao.EhTerminal(terminal).Should().BeTrue();

        foreach (var papel in Enum.GetValues<PapelEventoRegulacao>())
        {
            MaquinaDeEstadosRegulacao.DestinosDe(terminal, papel)
                .Should().BeEmpty($"{terminal} é terminal, inclusive para {papel}");
        }
    }

    [Fact]
    public void O_envio_assistido_salta_o_estado_de_envio_em_curso()
    {
        // "Registrar envio" é o agente digitando um número gerado fora: não há envio nosso em
        // curso para travar, então o estado intermediário não se aplica.
        MaquinaDeEstadosRegulacao
            .EventoDe(StatusRegulacao.EmAnalise, StatusRegulacao.EnviadaAoSistema, PapelEventoRegulacao.Agente)
            .Should().Be(TipoEventoRegulacao.NumeroExterno);

        // Já o envio pelo robô passa por EnviandoAoSistema — é ele que trava o duplo envio.
        MaquinaDeEstadosRegulacao
            .EventoDe(StatusRegulacao.EmAnalise, StatusRegulacao.EnviandoAoSistema, PapelEventoRegulacao.Agente)
            .Should().Be(TipoEventoRegulacao.EnvioSistema);
    }

    [Fact]
    public void Transicao_inexistente_nao_tem_evento()
    {
        MaquinaDeEstadosRegulacao
            .EventoDe(StatusRegulacao.Rascunho, StatusRegulacao.Concluida, PapelEventoRegulacao.Agente)
            .Should().BeNull("sem evento, quem chama recusa em vez de gravar uma linha genérica");
    }

    [Fact]
    public void A_situacao_externa_pode_ir_e_voltar_entre_fila_e_agendada()
    {
        // O sistema de lá desmarca e remarca; nossa trilha acompanha sem travar.
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.EmFilaExterna, StatusRegulacao.Agendada, PapelEventoRegulacao.Sistema)
            .Should().BeTrue();
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.Agendada, StatusRegulacao.EmFilaExterna, PapelEventoRegulacao.Sistema)
            .Should().BeTrue();
    }
}
