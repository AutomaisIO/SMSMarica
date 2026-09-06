using SMSMais.Core.Integracoes.SisregWeb.Historico;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Regras do motor que traz o passado da agenda, uma fatia de 31 dias por vez.
///
/// <para>Duas delas custam caro se estiverem erradas e não dão sintoma nenhum na hora:</para>
///
/// <para><b>1. Fatia que falhou não pode avançar a cobertura.</b> A tela passaria a dizer "coberto
/// desde tal data" sobre um período que nunca foi importado. O buraco é invisível — ninguém tem como
/// desconfiar de um intervalo que se declara completo — e contamina toda análise de ocupação feita
/// em cima dele. Repetir a fatia custa uma requisição.</para>
///
/// <para><b>2. Onde a unidade começa quem decide é o dado, não o cadastro.</b> O Centro Materno
/// Infantil declara escala desde 10/09/1986; seguir essa data custaria 472 requisições procurando
/// agendamento de 40 anos atrás que nunca existiu. Por isso o motor para por fatias consecutivas
/// vazias — e o contador precisa <b>zerar</b> assim que aparece registro, senão um hiato de férias
/// somado a outro de reforma encerraria a unidade no meio do histórico dela.</para>
/// </summary>
public class DecididorHistoricoTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 4);
    private const int Fatia = 31;
    private const int ParaConcluir = 6;

    // ---------------------------------------------------------------- fatia

    /// <summary>
    /// Sem cobertura, a primeira fatia termina ONTEM e não em "hoje − 31": a varredura diária cobre
    /// o futuro e o dia corrente, e começar mais atrás deixaria um vão entre os dois motores.
    /// </summary>
    [Fact]
    public void Primeira_fatia_emenda_em_ontem()
    {
        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, Fatia);

        Assert.Equal(new DateOnly(2026, 9, 3), fim);
        Assert.Equal(new DateOnly(2026, 8, 4), inicio);
        Assert.Equal(Fatia - 1, fim.DayNumber - inicio.DayNumber);
    }

    /// <summary>Fatias encostam sem sobrepor e sem deixar buraco: cada uma termina no dia anterior
    /// ao início da cobertura. Um dia de folga aqui viraria um dia perdido por mês de histórico.</summary>
    [Fact]
    public void Fatias_seguintes_encostam_sem_buraco_e_sem_sobreposicao()
    {
        var (inicio1, _) = DecididorHistorico.ProximaFatia(null, Hoje, Fatia);
        var (_, fim2) = DecididorHistorico.ProximaFatia(inicio1, Hoje, Fatia);

        Assert.Equal(inicio1.AddDays(-1), fim2);
    }

    /// <summary>O SISREG recusa intervalo maior que 31 dias — a fatia nunca pode passar disso.</summary>
    [Fact]
    public void Fatia_respeita_o_teto_do_sisreg()
    {
        var (inicio, fim) = DecididorHistorico.ProximaFatia(new DateOnly(2025, 1, 1), Hoje, Fatia);

        Assert.True(fim.DayNumber - inicio.DayNumber < 31);
    }

    // ---------------------------------------------------------------- decisão

    [Fact]
    public void Fatia_nunca_pedida_e_pedida()
    {
        Assert.Equal(
            PassoHistorico.Pedir,
            DecididorHistorico.Decidir(null, 0, 0, ParaConcluir));
    }

    [Theory]
    [InlineData(StatusVarredura.Pendente)]
    [InlineData(StatusVarredura.EmExecucao)]
    public void Fatia_rodando_espera(StatusVarredura status)
    {
        Assert.Equal(
            PassoHistorico.Esperar,
            DecididorHistorico.Decidir(status, 0, 0, ParaConcluir));
    }

    /// <summary>
    /// O caso que protege a integridade da cobertura. Erro, CAPTCHA e cancelamento têm de repetir —
    /// avançar sobre qualquer um deles cria um período que se diz importado e não está.
    /// </summary>
    [Theory]
    [InlineData(StatusVarredura.Erro)]
    [InlineData(StatusVarredura.Parcial)]
    [InlineData(StatusVarredura.Cancelada)]
    public void Fatia_que_terminou_mal_repete_sem_avancar_a_cobertura(StatusVarredura status)
    {
        // Passado o recuo entre tentativas. Repetir na hora fazia um erro determinístico —
        // unidade sem permissão, procedimento recusado — repetir a cada tick de 90 s.
        var opcoes = new HistoricoOpcoes();
        var idade = TimeSpan.FromMinutes(opcoes.MinutosEntreTentativas + 1);

        Assert.Equal(
            PassoHistorico.Repetir,
            DecididorHistorico.Decidir(status, 0, 0, ParaConcluir, idade, 0, opcoes));
    }

    /// <summary>
    /// Falha RECENTE espera o recuo antes de tentar de novo.
    ///
    /// <para>Sem o recuo, uma janela que falha sempre repete a cada tick e queima o orçamento
    /// anti-robô do operador — cujo estouro pausa a unidade por 24 h.</para>
    /// </summary>
    [Fact]
    public void Falha_recente_recua_antes_de_repetir()
    {
        var opcoes = new HistoricoOpcoes();

        Assert.Equal(
            PassoHistorico.Esperar,
            DecididorHistorico.Decidir(
                StatusVarredura.Erro, 0, 0, ParaConcluir, TimeSpan.FromMinutes(1), 0, opcoes));
    }

    /// <summary>
    /// Execução "rodando" ainda NOVA é rodando mesmo; só a antiga é abandonada.
    ///
    /// <para>Distinguir por idade, e não por "tem algo vivo agora", é o que impede o laço de
    /// 06/09/2026: a linha nasce antes de o runner se registrar como vivo, então a leitura por
    /// liveness classificava como órfã uma execução que tinha acabado de começar.</para>
    /// </summary>
    [Fact]
    public void Rodando_ha_pouco_espera_e_rodando_ha_muito_e_abandonada()
    {
        var opcoes = new HistoricoOpcoes();

        Assert.Equal(
            PassoHistorico.Esperar,
            DecididorHistorico.Decidir(
                StatusVarredura.EmExecucao, 0, 0, ParaConcluir, TimeSpan.FromMinutes(5), 0, opcoes));

        Assert.Equal(
            PassoHistorico.Repetir,
            DecididorHistorico.Decidir(
                StatusVarredura.EmExecucao, 0, 0, ParaConcluir,
                TimeSpan.FromMinutes(opcoes.MinutosParaAbandonada + 1), 0, opcoes));
    }

    /// <summary>Teto de tentativas na mesma fatia: desliga em vez de insistir.</summary>
    [Fact]
    public void Tentativas_demais_na_mesma_fatia_bloqueiam()
    {
        var opcoes = new HistoricoOpcoes();

        Assert.Equal(
            PassoHistorico.Bloquear,
            DecididorHistorico.Decidir(
                StatusVarredura.Erro, 0, 0, ParaConcluir, TimeSpan.FromHours(2),
                opcoes.TentativasPorFatia, opcoes));
    }

    [Fact]
    public void Fatia_com_registro_avanca()
    {
        Assert.Equal(
            PassoHistorico.Avancar,
            DecididorHistorico.Decidir(StatusVarredura.Concluida, 42, 0, ParaConcluir));
    }

    /// <summary>Vazia antes do limite ainda avança — é assim que se atravessa um hiato curto.</summary>
    [Fact]
    public void Fatia_vazia_antes_do_limite_ainda_avanca()
    {
        Assert.Equal(
            PassoHistorico.Avancar,
            DecididorHistorico.Decidir(StatusVarredura.Concluida, 0, fatiasVaziasAtuais: 4, ParaConcluir));
    }

    /// <summary>Na sexta vazia seguida (≈ meio ano), a unidade chegou ao começo dela.</summary>
    [Fact]
    public void Sexta_fatia_vazia_seguida_conclui_a_unidade()
    {
        Assert.Equal(
            PassoHistorico.Concluir,
            DecididorHistorico.Decidir(StatusVarredura.Concluida, 0, fatiasVaziasAtuais: 5, ParaConcluir));
    }

    /// <summary>
    /// Um registro no meio zera a contagem. Sem isto, dois hiatos separados — férias coletivas e
    /// depois uma reforma — somariam e encerrariam a unidade no meio do histórico dela, sem erro
    /// nenhum e sem ninguém perceber que faltou dado.
    /// </summary>
    [Fact]
    public void Registro_no_meio_zera_a_contagem_de_vazias()
    {
        Assert.Equal(0, DecididorHistorico.ProximasVazias(atuais: 5, registrosEncontrados: 1));
        Assert.Equal(6, DecididorHistorico.ProximasVazias(atuais: 5, registrosEncontrados: 0));
    }

    /// <summary>Configuração zerada não pode virar laço infinito nem parada imediata.</summary>
    [Fact]
    public void Limite_zerado_nao_quebra_a_parada()
    {
        Assert.Equal(
            PassoHistorico.Concluir,
            DecididorHistorico.Decidir(StatusVarredura.Concluida, 0, 0, fatiasParaConcluir: 0));

        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, diasPorFatia: 0);
        Assert.Equal(inicio, fim);
    }
}
