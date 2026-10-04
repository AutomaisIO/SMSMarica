using FluentAssertions;
using SMSMais.Core.Worklist.Background;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// 04/10/2026: o restart semanal do dcm4chee fechou a porta 8080 por 18 s e o sincronizador mandou
/// aviso de "Connection refused". Só falha contínua acima da tolerância vira erro.
/// </summary>
public class ToleranciaFalhaTests
{
    private static readonly DateTime T0 = new(2026, 10, 4, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Restart_do_pacs_de_menos_de_dois_minutos_nao_vira_erro()
    {
        var t = new ToleranciaFalha(TimeSpan.FromMinutes(2));

        t.RegistrarFalha(T0).Should().BeFalse();
        t.RegistrarFalha(T0.AddSeconds(30)).Should().BeFalse();
        t.RegistrarSucesso(T0.AddSeconds(60)).Should().BeNull("ficou na tolerância: nada a anunciar");
        t.FalhandoDesde.Should().BeNull();
    }

    [Fact]
    public void Falha_continua_a_partir_de_dois_minutos_vira_erro()
    {
        var t = new ToleranciaFalha(TimeSpan.FromMinutes(2));

        // Passagens de 30 s: 0, 30, 60, 90 ainda toleradas; 120 escala.
        for (var s = 0; s < 120; s += 30)
            t.RegistrarFalha(T0.AddSeconds(s)).Should().BeFalse();
        t.RegistrarFalha(T0.AddSeconds(120)).Should().BeTrue();
        t.RegistrarFalha(T0.AddSeconds(150)).Should().BeTrue();

        t.RegistrarSucesso(T0.AddSeconds(180)).Should().Be(TimeSpan.FromSeconds(180));
    }

    [Fact]
    public void Passagem_boa_no_meio_zera_a_contagem()
    {
        var t = new ToleranciaFalha(TimeSpan.FromMinutes(2));

        t.RegistrarFalha(T0);
        t.RegistrarFalha(T0.AddSeconds(90));
        t.RegistrarSucesso(T0.AddSeconds(100));

        // Nova sequência: conta de novo a partir daqui, não do T0.
        t.RegistrarFalha(T0.AddSeconds(130)).Should().BeFalse();
        t.FalhandoDesde.Should().Be(T0.AddSeconds(130));
    }

    [Fact]
    public void Tolerancia_zero_e_o_comportamento_antigo() =>
        new ToleranciaFalha(TimeSpan.Zero).RegistrarFalha(T0).Should().BeTrue();
}
