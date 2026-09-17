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
}
