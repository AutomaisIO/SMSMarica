using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Dtos;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// "Rodando" na tela tem que significar que alguém está rodando.
///
/// <para><b>O incidente (08/09/2026).</b> Uma varredura do CDT ficou <b>65 minutos</b> anunciando
/// "Rodando" depois que dois deploys reiniciaram o serviço embaixo dela. O operador olhou um
/// contador parado em "24 requisições · 0 importadas" sem ter como distinguir lentidão de morte —
/// e a corrida estava morta desde o primeiro deploy.</para>
///
/// <para><b>Por que durava tanto.</b> O critério de abandono era a <b>idade</b>
/// (<c>IniciadoEm</c>), e idade não separa "longa e saudável" de "morta". Como uma varredura
/// legítima leva 20–45 min, o corte tinha de ser generoso — e a faxina ainda por cima só rodava
/// quando alguém iniciava a próxima varredura <i>daquela unidade</i>.</para>
///
/// <para><b>Por que estes testes existem em par.</b> O erro oposto é pior e já foi cometido: em
/// 06/09/2026, dar por morta uma corrida viva produziu um laço que a matava e queimava 57
/// requisições por hora sempre na mesma fatia. Então cada teste de "marca como sem sinal" tem ao
/// lado um de "NÃO marca" — corrida que bate, mesmo há horas, continua verde.</para>
/// </summary>
public class SemSinalDaVarreduraTests
{
    private const int MinutosSemSinal = 3;
    private const int MinutosParaAbandonada = 45;

    private static readonly Guid Viva = Guid.Parse("01a08239-a287-7455-a28f-bd720978034b");

    /// <summary>
    /// Réplica exata da régua aplicada em <c>ListarExecucoesAsync</c>. Fica aqui, e não no serviço,
    /// porque a listagem depende de <c>DbContext</c>, unidade atual e estado vivo — montar tudo
    /// isso testaria o encanamento, não a decisão. O que precisa de rede de proteção é a decisão.
    /// </summary>
    private static bool SemSinal(SisregVarreduraExecucao e, Guid? execucaoViva, DateTime agora)
    {
        if (e.Status is not (StatusVarredura.Pendente or StatusVarredura.EmExecucao)) return false;
        if (execucaoViva == e.Id) return false;

        var referencia = e.UltimoSinalEm ?? e.IniciadoEm;
        var limite = e.UltimoSinalEm is null
            ? TimeSpan.FromMinutes(MinutosParaAbandonada)
            : TimeSpan.FromMinutes(MinutosSemSinal);

        return agora - referencia > limite;
    }

    private static SisregVarreduraExecucao Execucao(
        StatusVarredura status = StatusVarredura.EmExecucao,
        int iniciadaHaMin = 5,
        int? batidaHaMin = 0,
        Guid? id = null)
    {
        var agora = new DateTime(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc);
        return new SisregVarreduraExecucao
        {
            Id = id ?? Guid.CreateVersion7(),
            Status = status,
            IniciadoEm = agora.AddMinutes(-iniciadaHaMin),
            UltimoSinalEm = batidaHaMin is { } m ? agora.AddMinutes(-m) : null,
        };
    }

    private static readonly DateTime Agora = new(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------------ o que o incidente exigia

    /// <summary>O caso exato de 08/09: viva no banco, muda há muito, e ninguém rodando.</summary>
    [Fact]
    public void Execucao_que_parou_de_bater_aparece_sem_sinal()
    {
        var e = Execucao(iniciadaHaMin: 65, batidaHaMin: 60);

        Assert.True(SemSinal(e, execucaoViva: null, Agora));
    }

    /// <summary>Três batimentos perdidos bastam — não é preciso esperar os 45 min da idade.</summary>
    [Fact]
    public void Silencio_de_poucos_minutos_ja_denuncia()
    {
        Assert.True(SemSinal(Execucao(iniciadaHaMin: 6, batidaHaMin: 4), null, Agora));
    }

    // ------------------------------------------------------------- o erro oposto, que é pior

    /// <summary>
    /// <b>A corrida longa e saudável continua verde.</b> É o que o critério de idade não conseguia
    /// fazer: com 45 min de teto, uma varredura de janela grande virava "abandonada" sozinha.
    /// </summary>
    [Fact]
    public void Corrida_de_horas_que_continua_batendo_nao_e_sem_sinal()
    {
        Assert.False(SemSinal(Execucao(iniciadaHaMin: 180, batidaHaMin: 0), null, Agora));
    }

    /// <summary>A execução que ESTÁ viva nesta instância nunca é marcada, bata ou não.</summary>
    [Fact]
    public void Execucao_viva_na_memoria_nunca_e_marcada()
    {
        var e = Execucao(iniciadaHaMin: 200, batidaHaMin: 90, id: Viva);

        Assert.False(SemSinal(e, execucaoViva: Viva, Agora));
    }

    /// <summary>Execução encerrada não tem sinal para dar — e não pode aparecer de amarelo.</summary>
    [Theory]
    [InlineData(StatusVarredura.Concluida)]
    [InlineData(StatusVarredura.Parcial)]
    [InlineData(StatusVarredura.Erro)]
    [InlineData(StatusVarredura.Cancelada)]
    public void Execucao_ja_encerrada_nunca_e_sem_sinal(StatusVarredura status)
    {
        Assert.False(SemSinal(Execucao(status, iniciadaHaMin: 500, batidaHaMin: 500), null, Agora));
    }

    // ------------------------------------------------------- histórico anterior ao batimento

    /// <summary>
    /// Execução sem batimento (anterior à coluna) cai no critério ANTIGO, a idade. Tratar ausência
    /// de batimento como morte carimbaria de amarelo todo o histórico já gravado.
    /// </summary>
    [Fact]
    public void Sem_batimento_e_recente_nao_e_marcada_pelo_criterio_novo()
    {
        Assert.False(SemSinal(Execucao(iniciadaHaMin: 10, batidaHaMin: null), null, Agora));
    }

    [Fact]
    public void Sem_batimento_e_velha_cai_no_criterio_de_idade()
    {
        Assert.True(SemSinal(Execucao(iniciadaHaMin: 46, batidaHaMin: null), null, Agora));
    }
}
