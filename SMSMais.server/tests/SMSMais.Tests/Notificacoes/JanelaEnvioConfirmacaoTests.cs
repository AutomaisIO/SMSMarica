using SMSMais.Core.Notificacoes.Confirmacoes;

namespace SMSMais.Tests.Notificacoes;

/// <summary>Janela de horário da confirmação (Brasília = UTC-3): 08:00 inclusivo, 18:00 exclusivo.</summary>
public class JanelaEnvioConfirmacaoTests
{
    private static readonly TimeOnly Inicio = new(8, 0);
    private static readonly TimeOnly Fim = new(18, 0);

    private static DateTime Utc(int dia, int hora, int minuto = 0) =>
        new(2026, 9, dia, hora, minuto, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(11, 0, true)]   // 08:00 Brasília
    [InlineData(20, 59, true)]  // 17:59
    [InlineData(21, 0, false)]  // 18:00 — já não sai
    [InlineData(10, 59, false)] // 07:59
    [InlineData(2, 0, false)]   // 23:00 da véspera
    public void Dentro_respeita_os_limites(int horaUtc, int minuto, bool esperado) =>
        Assert.Equal(esperado, JanelaEnvioConfirmacao.Dentro(Utc(17, horaUtc, minuto), Inicio, Fim));

    [Fact]
    public void Depois_das_18h_a_proxima_abertura_e_as_8h_do_dia_seguinte()
    {
        // 17/09 19:30 Brasília = 22:30 UTC → abre 18/09 08:00 Brasília = 11:00 UTC.
        Assert.Equal(Utc(18, 11), JanelaEnvioConfirmacao.ProximaAbertura(Utc(17, 22, 30), Inicio, Fim));
    }

    [Fact]
    public void De_madrugada_a_proxima_abertura_e_as_8h_do_mesmo_dia()
    {
        // 18/09 05:00 Brasília = 08:00 UTC → abre 18/09 08:00 Brasília.
        Assert.Equal(Utc(18, 11), JanelaEnvioConfirmacao.ProximaAbertura(Utc(18, 8), Inicio, Fim));
    }

    [Fact]
    public void Dentro_da_janela_a_proxima_abertura_e_agora()
    {
        var agora = Utc(17, 15);
        Assert.Equal(agora, JanelaEnvioConfirmacao.ProximaAbertura(agora, Inicio, Fim));
    }

    // ---- Folga da véspera (08/10/2026): atendimento de amanhã sai até as 21h ----

    private static readonly TimeOnly LimiteVespera = new(21, 0);

    [Theory]
    [InlineData(21, 50, 18, true)]  // 18:50 Brasília, atendimento amanhã — o caso da importação das 18h
    [InlineData(23, 59, 18, true)]  // 20:59
    [InlineData(0, 0, 18, false)]   // 21:00 — a folga acabou (já é 18/09 00:00 UTC, mas 17/09 em Brasília)
    [InlineData(21, 50, 19, false)] // atendimento depois de amanhã: espera a janela normal
    [InlineData(21, 50, 17, false)] // atendimento hoje: a folga não é para o próprio dia
    [InlineData(10, 30, 18, false)] // 07:30 da manhã: a folga não antecipa o início da janela
    public void Folga_da_vespera_so_vale_depois_do_fim_e_para_amanha(int horaUtc, int minuto, int diaAtendimento, bool esperado)
    {
        // Agora: 17/09 (ou 18/09 00:00 UTC = 17/09 21:00 Brasília). Atendimento às 09:30 Brasília.
        var agora = horaUtc == 0 ? Utc(18, 0) : Utc(17, horaUtc, minuto);
        var atendimento = Utc(diaAtendimento, 12, 30);
        Assert.Equal(esperado, JanelaEnvioConfirmacao.NaFolgaDaVespera(agora, atendimento, Fim, LimiteVespera));
    }

    [Fact]
    public void Folga_desligada_quando_a_janela_ja_vai_alem_do_limite() =>
        Assert.False(JanelaEnvioConfirmacao.NaFolgaDaVespera(
            Utc(17, 23, 30), Utc(18, 12, 30), fim: new TimeOnly(22, 0), LimiteVespera));

    [Theory]
    [InlineData(11, 30, true)]  // 17/09 08:30 Brasília, atendimento 17/09 09:30
    [InlineData(2, 0, false)]   // 17/09 23:00 Brasília (18/09 02:00 UTC) — hoje é 17, atendimento é 18
    public void Atendimento_hoje_compara_datas_de_Brasilia(int horaUtcAgora, int minuto, bool esperado)
    {
        var agora = horaUtcAgora == 2 ? Utc(18, 2) : Utc(17, horaUtcAgora, minuto);
        var atendimento = horaUtcAgora == 2 ? Utc(18, 12, 30) : Utc(17, 12, 30);
        Assert.Equal(esperado, JanelaEnvioConfirmacao.AtendimentoHoje(agora, atendimento));
    }
}
