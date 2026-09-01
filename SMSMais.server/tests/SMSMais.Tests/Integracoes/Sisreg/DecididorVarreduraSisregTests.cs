using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A decisão do scheduler da varredura. O teste mais importante aqui é o do
/// <c>ProximoRunEm == null</c>: no PEP isso significa "elegível já", e copiar aquela semântica
/// faria o primeiro tick depois de um deploy varrer no meio da tarde — derrubando a sessão do
/// atendente da unidade, porque o SISREG só aceita um login por operador.
/// </summary>
public class DecididorVarreduraSisregTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    // Regra atual: pode iniciar EXCETO na faixa (07:30, 15:00] — o SISREG bloqueia o expo_solicitacoes
    // das 08:00 às 15:00, e a margem de 30 min antecipa o corte de entrada para 07:30.
    private static readonly TimeOnly CorteEntrada = new(7, 30);
    private static readonly TimeOnly BloqueioFim = new(15, 0);

    /// <summary>03/08/2026 04:00 em Brasília = 07:00 UTC. Fora do bloqueio 08:00–15:00.</summary>
    private static readonly DateTime AgoraUtc = new(2026, 8, 3, 7, 0, 0, DateTimeKind.Utc);
    private static readonly TimeOnly HoraLocal = new(4, 0);

    /// <param name="semProximoRun">Parâmetro próprio em vez de <c>proximoRunEm: null</c>: com o
    /// default aplicado por <c>??</c>, o null do chamador seria engolido e o teste passaria à toa.</param>
    private static SisregVarreduraAgenda Agenda(
        bool ativo = true,
        DateTime? proximoRunEm = null,
        bool semProximoRun = false,
        DateTime? pausadoAte = null,
        int falhas = 0,
        TimeOnly? horaLocal = null) =>
        new()
        {
            UnidadeId = Guid.NewGuid(),
            Ativo = ativo,
            HoraLocal = horaLocal ?? new TimeOnly(4, 30),
            DiasAFrente = 21,
            ProximoRunEm = semProximoRun ? null : proximoRunEm ?? AgoraUtc.AddMinutes(-1),
            PausadoAte = pausadoAte,
            FalhasConsecutivas = falhas,
        };

    private static DecisaoVarredura Decidir(
        SisregVarreduraAgenda agenda,
        TimeOnly? horaLocal = null,
        bool varreduraViva = false,
        bool importacaoViva = false) =>
        DecididorVarreduraSisreg.Decidir(
            agenda, AgoraUtc, horaLocal ?? HoraLocal, CorteEntrada, BloqueioFim, varreduraViva, importacaoViva);

    [Fact]
    public void Agenda_vencida_dentro_da_janela_dispara()
    {
        Assert.Equal(DecisaoVarredura.Disparar, Decidir(Agenda()));
    }

    [Fact]
    public void Proximo_run_nulo_NAO_dispara()
    {
        // A inversão deliberada em relação ao PEP. Se isto virar "Disparar", o deploy do dia
        // seguinte varre no expediente.
        Assert.Equal(DecisaoVarredura.Aguardar, Decidir(Agenda(semProximoRun: true)));
    }

    [Fact]
    public void Agenda_desligada_nao_dispara()
    {
        Assert.Equal(DecisaoVarredura.Aguardar, Decidir(Agenda(ativo: false)));
    }

    [Fact]
    public void Antes_da_hora_nao_dispara()
    {
        Assert.Equal(DecisaoVarredura.Aguardar, Decidir(Agenda(proximoRunEm: AgoraUtc.AddHours(1))));
    }

    [Fact]
    public void Pausada_pelo_captcha_nao_dispara()
    {
        Assert.Equal(DecisaoVarredura.Aguardar, Decidir(Agenda(pausadoAte: AgoraUtc.AddHours(20))));
    }

    [Fact]
    public void Pausa_vencida_volta_a_disparar()
    {
        Assert.Equal(DecisaoVarredura.Disparar, Decidir(Agenda(pausadoAte: AgoraUtc.AddMinutes(-1))));
    }

    [Theory]
    [InlineData(7, 31)]  // logo após o corte de entrada
    [InlineData(8, 0)]   // início do bloqueio
    [InlineData(10, 0)]  // meio da manhã
    [InlineData(14, 30)] // meio da tarde
    [InlineData(15, 0)]  // fim do bloqueio — 15:00 ainda recusa
    public void Dentro_do_bloqueio_nao_dispara(int hora, int minuto)
    {
        Assert.Equal(DecisaoVarredura.ForaDaJanela, Decidir(Agenda(), new TimeOnly(hora, minuto)));
    }

    [Theory]
    [InlineData(7, 30)]  // exatamente no corte — ainda entra
    [InlineData(3, 0)]   // madrugada
    [InlineData(15, 1)]  // um minuto depois do bloqueio
    [InlineData(18, 0)]  // fim de tarde
    [InlineData(23, 30)] // noite
    public void Fora_do_bloqueio_dispara(int hora, int minuto)
    {
        Assert.Equal(DecisaoVarredura.Disparar, Decidir(Agenda(), new TimeOnly(hora, minuto)));
    }

    [Theory]
    [InlineData(7, 30, true)]   // corte inclusivo
    [InlineData(7, 31, false)]
    [InlineData(8, 0, false)]
    [InlineData(15, 0, false)]  // fim do bloqueio inclusivo
    [InlineData(15, 1, true)]
    [InlineData(0, 0, true)]
    public void PodeIniciar_recusa_so_a_faixa_do_bloqueio(int hora, int minuto, bool podeIniciar)
    {
        Assert.Equal(podeIniciar, DecididorVarreduraSisreg.PodeIniciar(new TimeOnly(hora, minuto), CorteEntrada, BloqueioFim));
    }

    [Fact]
    public void Importacao_manual_viva_faz_o_robo_esperar()
    {
        // O humano sempre ganha: as duas saem para o SISREG pela mesma rota.
        Assert.Equal(DecisaoVarredura.PularRunVivo, Decidir(Agenda(), importacaoViva: true));
    }

    [Fact]
    public void Varredura_viva_faz_o_robo_esperar()
    {
        Assert.Equal(DecisaoVarredura.PularRunVivo, Decidir(Agenda(), varreduraViva: true));
    }

    // ------------------------------------------------------------------ próximo disparo

    [Fact]
    public void Proximo_diario_e_hoje_quando_a_hora_ainda_nao_passou()
    {
        // 03/08 04:00 local; alvo 04:30 → hoje mesmo.
        var proximo = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(4, 30), AgoraUtc, Brasilia);
        var local = TimeZoneInfo.ConvertTimeFromUtc(proximo, Brasilia);

        Assert.Equal(new DateTime(2026, 8, 3, 4, 30, 0), local);
    }

    [Fact]
    public void Proximo_diario_pula_para_amanha_quando_a_hora_ja_passou()
    {
        var proximo = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(3, 0), AgoraUtc, Brasilia);
        var local = TimeZoneInfo.ConvertTimeFromUtc(proximo, Brasilia);

        Assert.Equal(new DateTime(2026, 8, 4, 3, 0, 0), local);
    }

    [Fact]
    public void Hora_exatamente_igual_a_agora_vai_para_amanha()
    {
        // Estritamente depois: senão o mesmo tick redispararia em loop.
        var proximo = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(4, 0), AgoraUtc, Brasilia);
        var local = TimeZoneInfo.ConvertTimeFromUtc(proximo, Brasilia);

        Assert.Equal(new DateTime(2026, 8, 4, 4, 0, 0), local);
    }

    // ------------------------------------------------------------------ backoff

    [Fact]
    public void Backoff_cresce_com_as_falhas()
    {
        // Hora diária distante (23:00, ~19h à frente) para o crescimento aparecer antes do limite
        // do horário diário entrar em cena — que é o que o teste seguinte cobre.
        var longe = new TimeOnly(23, 0);
        var uma = DecididorVarreduraSisreg.ProximoAposErro(Agenda(falhas: 1, horaLocal: longe), AgoraUtc, Brasilia);
        var duas = DecididorVarreduraSisreg.ProximoAposErro(Agenda(falhas: 2, horaLocal: longe), AgoraUtc, Brasilia);

        Assert.True(duas > uma);
    }

    [Fact]
    public void Backoff_e_limitado_pelo_horario_diario_mesmo_com_poucas_falhas()
    {
        // Hora diária logo ali (04:30, 30 min à frente): o backoff de 30 min de 1 falha já bate no
        // limite. Não é bug — é o motor preferindo o horário combinado a um retry arbitrário.
        var proximo = DecididorVarreduraSisreg.ProximoAposErro(Agenda(falhas: 1), AgoraUtc, Brasilia);
        var diario = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(4, 30), AgoraUtc, Brasilia);

        Assert.Equal(diario, proximo);
    }

    [Fact]
    public void Backoff_nunca_ultrapassa_o_proximo_horario_diario()
    {
        // Sem este limite, falhas repetidas empurrariam a unidade para depois do dia seguinte e ela
        // pararia de ser varrida sem ninguém perceber.
        var proximo = DecididorVarreduraSisreg.ProximoAposErro(Agenda(falhas: 99), AgoraUtc, Brasilia);
        var diario = DecididorVarreduraSisreg.ProximoDiario(new TimeOnly(4, 30), AgoraUtc, Brasilia);

        Assert.True(proximo <= diario);
    }
}
