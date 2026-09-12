using System.Text.Json.Nodes;
using SMSMais.Core.Integracoes.SisregWeb.Fila;
using SMSMais.Core.Integracoes.SisregWeb.Fila.Background;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Releitura completa diária da fila (decidida em 12/09/2026) e o que a tela mostra da leitura.
/// Sem banco: é regra de configuração e de estado em memória.
/// </summary>
public class FilaAgendamentoTests
{
    /// <summary>
    /// Sem configuração, a releitura vem LIGADA e de madrugada: é o que mantém a fila verdadeira
    /// (cancelados, reenviados, troca de risco). Desligar é decisão de alguém.
    /// </summary>
    [Fact]
    public void Sem_configuracao_a_releitura_vem_ligada_as_3h()
    {
        var a = AgendamentoDaFila.Ler(null);

        Assert.True(a.Ativo);
        Assert.Equal("03:00", a.HoraLocal);
    }

    [Fact]
    public void Le_o_que_foi_salvo()
    {
        var json = new JsonObject
        {
            [AgendamentoDaFila.ChaveAtivo] = false,
            [AgendamentoDaFila.ChaveHora] = "02:15",
        };

        var a = AgendamentoDaFila.Ler(json);

        Assert.False(a.Ativo);
        Assert.Equal("02:15", a.HoraLocal);
    }

    /// <summary>Hora estragada no JSON não pode derrubar o agendador: vale o padrão.</summary>
    [Fact]
    public void Hora_invalida_cai_no_padrao()
    {
        var a = AgendamentoDaFila.Ler(new JsonObject { [AgendamentoDaFila.ChaveHora] = "25:99" });

        Assert.Equal("03:00", a.HoraLocal);
    }

    private static LeituraFilaDto Lida(JanelaFila j) => new(j.Inicio, j.Fim, 10, 0, 10, 0, 0, 2);

    private static readonly ResumoFilaDto Resumo = new(100, DateTime.UtcNow);

    /// <summary>
    /// Em 12/09/2026 a tela mostrava "última leitura interrompida" para uma janela relida com
    /// sucesso 22 s depois — parecia que a carga tinha falhado. Falha resolvida na nova tentativa é
    /// informação (contada), não erro.
    /// </summary>
    [Fact]
    public void Falha_resolvida_na_nova_tentativa_nao_aparece_como_erro()
    {
        var estado = new FilaPendenteEstadoVivo();
        var janelas = JanelasDaFila.Completa(new DateOnly(2026, 9, 12), new DateOnly(2026, 7, 1));
        estado.Enfileirar(janelas, completa: true);

        var j = estado.Proxima()!;
        estado.Falhar("sessão caída", desistir: false);
        Assert.Equal(j, estado.Proxima());           // a mesma janela volta para a frente
        estado.Concluir(Lida(j));

        var s = estado.Snapshot(Resumo);
        Assert.Null(s.UltimoErro);
        Assert.Equal(1, s.RelidasAposFalha);
        Assert.True(s.EmExecucao);
    }

    /// <summary>Três falhas seguidas: a leitura desiste — aí sim é erro, e alguém precisa saber.</summary>
    [Fact]
    public void Tres_falhas_seguidas_desistem_e_aparecem_como_erro()
    {
        var estado = new FilaPendenteEstadoVivo();
        estado.Enfileirar(JanelasDaFila.Recente(new DateOnly(2026, 9, 12)), completa: false);

        for (var i = 0; i < 3; i++)
        {
            Assert.NotNull(estado.Proxima());
            estado.Falhar($"falha {i + 1}", desistir: false);
        }

        var s = estado.Snapshot(Resumo);
        Assert.False(s.EmExecucao);
        Assert.Equal("falha 3", s.UltimoErro);
    }
}
