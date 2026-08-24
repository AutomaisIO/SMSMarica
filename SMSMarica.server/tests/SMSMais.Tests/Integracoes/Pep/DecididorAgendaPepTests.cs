using SMSMais.Core.Integracoes.Pep.Background;
using SMSMais.Data.Entities.Pep;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Lógica pura de decisão do scheduler contínuo (ADR-0024): elegibilidade, janela local
/// (inclusive cruzando a meia-noite), backoff exponencial com teto e re-scan de médicos.
/// </summary>
public class DecididorAgendaPepTests
{
    private static readonly DateTime Agora = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeOnly NoveBrt = new(9, 0);

    private static PepSincronizacaoAgenda Agenda(Action<PepSincronizacaoAgenda>? ajusta = null)
    {
        var a = new PepSincronizacaoAgenda { FonteId = Guid.NewGuid(), Ativo = true, IntervaloMinutos = 30 };
        ajusta?.Invoke(a);
        return a;
    }

    [Fact]
    public void Dispara_quando_ativa_sem_pendencias()
        => Assert.Equal(DecisaoAgenda.Disparar, DecididorAgendaPep.Decidir(Agenda(), Agora, runVivo: false, NoveBrt));

    [Fact]
    public void Aguarda_quando_inativa()
        => Assert.Equal(DecisaoAgenda.Aguardar, DecididorAgendaPep.Decidir(Agenda(a => a.Ativo = false), Agora, false, NoveBrt));

    [Fact]
    public void Aguarda_quando_proximo_run_no_futuro()
        => Assert.Equal(DecisaoAgenda.Aguardar,
            DecididorAgendaPep.Decidir(Agenda(a => a.ProximoRunEm = Agora.AddMinutes(5)), Agora, false, NoveBrt));

    [Fact]
    public void Dispara_quando_proximo_run_venceu()
        => Assert.Equal(DecisaoAgenda.Disparar,
            DecididorAgendaPep.Decidir(Agenda(a => a.ProximoRunEm = Agora.AddMinutes(-1)), Agora, false, NoveBrt));

    [Fact]
    public void Aguarda_durante_pausa_administrativa()
        => Assert.Equal(DecisaoAgenda.Aguardar,
            DecididorAgendaPep.Decidir(Agenda(a => a.PausadoAte = Agora.AddHours(1)), Agora, false, NoveBrt));

    [Fact]
    public void Pausa_vencida_nao_segura()
        => Assert.Equal(DecisaoAgenda.Disparar,
            DecididorAgendaPep.Decidir(Agenda(a => a.PausadoAte = Agora.AddHours(-1)), Agora, false, NoveBrt));

    [Fact]
    public void Pula_quando_ha_run_vivo_e_nao_compete()
        => Assert.Equal(DecisaoAgenda.PularRunVivo, DecididorAgendaPep.Decidir(Agenda(), Agora, runVivo: true, NoveBrt));

    [Theory]
    [InlineData(6, 0, 23, 0, 9, 0, true)]    // dentro da janela diurna
    [InlineData(6, 0, 23, 0, 23, 30, false)] // depois do fim
    [InlineData(6, 0, 23, 0, 5, 59, false)]  // antes do início
    [InlineData(22, 0, 5, 0, 23, 30, true)]  // janela cruzando a meia-noite — noite
    [InlineData(22, 0, 5, 0, 3, 0, true)]    // madrugada ainda dentro
    [InlineData(22, 0, 5, 0, 12, 0, false)]  // meio-dia fora
    public void Janela_local_inclusive_cruzando_meia_noite(int hi, int mi, int hf, int mf, int h, int m, bool dentro)
        => Assert.Equal(dentro, DecididorAgendaPep.DentroDaJanela(new TimeOnly(hi, mi), new TimeOnly(hf, mf), new TimeOnly(h, m)));

    [Fact]
    public void Sem_janela_configurada_roda_o_dia_inteiro()
        => Assert.True(DecididorAgendaPep.DentroDaJanela(null, null, new TimeOnly(3, 0)));

    [Fact]
    public void Fora_da_janela_e_o_motivo_quando_o_resto_esta_ok()
        => Assert.Equal(DecisaoAgenda.ForaDaJanela, DecididorAgendaPep.Decidir(
            Agenda(a => { a.JanelaInicioLocal = new TimeOnly(6, 0); a.JanelaFimLocal = new TimeOnly(8, 0); }),
            Agora, false, NoveBrt));

    [Theory]
    [InlineData(0, 30)]     // sem falha ainda: intervalo normal
    [InlineData(1, 60)]     // 30 × 2¹
    [InlineData(2, 120)]    // 30 × 2²
    [InlineData(3, 240)]    // 30 × 2³ = 4h — exatamente no teto
    [InlineData(4, 240)]    // teto de 4h segura
    [InlineData(10, 240)]
    public void Backoff_exponencial_com_teto_de_4h(int falhas, int minutosEsperados)
    {
        var agenda = Agenda(a => a.FalhasConsecutivas = falhas);
        var proximo = DecididorAgendaPep.ProximoAposErro(agenda, Agora);
        Assert.Equal(Agora.AddMinutes(minutosEsperados), proximo);
    }

    [Fact]
    public void Sucesso_reprograma_pelo_intervalo_normal()
        => Assert.Equal(Agora.AddMinutes(30), DecididorAgendaPep.ProximoAposSucesso(Agenda(), Agora));

    [Theory]
    [InlineData(null, true)]           // nunca escaneou → força
    [InlineData(-25.0, true)]          // marca com 25h e rescan de 24h → força
    [InlineData(-23.0, false)]         // marca com 23h → pula
    public void Rescan_de_medicos_por_idade_da_marca(double? horasAtras, bool esperado)
    {
        DateTime? marca = horasAtras is { } h ? Agora.AddHours(h) : null;
        Assert.Equal(esperado, DecididorAgendaPep.DeveForcarMedicos(marca, rescanHoras: 24, Agora));
    }
}
