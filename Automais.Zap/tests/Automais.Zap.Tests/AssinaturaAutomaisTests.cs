using System.Text;
using Automais.Zap.Core.Entregas;
using FluentAssertions;

namespace Automais.Zap.Tests;

public sealed class AssinaturaAutomaisTests
{
    private const string Segredo = "segredo-entre-nossos-sistemas";

    [Fact]
    public void Assina_no_formato_combinado()
    {
        var a = AssinaturaAutomais.Calcular(Segredo, 1_700_000_000, "{}"u8);
        a.Should().StartWith("sha256=");
        a[7..].Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Confere_o_proprio_corpo_e_o_proprio_instante()
    {
        var corpo = Encoding.UTF8.GetBytes("""{"entry":[]}""");
        var ts = 1_700_000_000L;
        AssinaturaAutomais.Confere(Segredo, ts, corpo, AssinaturaAutomais.Calcular(Segredo, ts, corpo))
            .Should().BeTrue();
    }

    [Fact]
    public void Recusa_corpo_adulterado()
    {
        var ts = 1_700_000_000L;
        var assinatura = AssinaturaAutomais.Calcular(Segredo, ts, "{\"a\":1}"u8);
        AssinaturaAutomais.Confere(Segredo, ts, "{\"a\":2}"u8, assinatura).Should().BeFalse();
    }

    [Fact]
    public void Recusa_o_mesmo_corpo_com_outro_instante()
    {
        // É isto que impede replay: capturar uma entrega não permite reenviá-la depois.
        var corpo = "{}"u8.ToArray();
        var assinatura = AssinaturaAutomais.Calcular(Segredo, 1_700_000_000, corpo);
        AssinaturaAutomais.Confere(Segredo, 1_700_000_060, corpo, assinatura).Should().BeFalse();
    }

    [Fact]
    public void Recusa_segredo_errado_e_assinatura_ausente()
    {
        var corpo = "{}"u8.ToArray();
        var assinatura = AssinaturaAutomais.Calcular(Segredo, 1L, corpo);
        AssinaturaAutomais.Confere("outro", 1L, corpo, assinatura).Should().BeFalse();
        AssinaturaAutomais.Confere(Segredo, 1L, corpo, null).Should().BeFalse();
        AssinaturaAutomais.Confere(Segredo, 1L, corpo, "").Should().BeFalse();
    }

    [Fact]
    public void Segredo_gerado_tem_entropia_de_sobra_e_nao_repete()
    {
        var a = AssinaturaAutomais.GerarSegredo();
        a.Should().MatchRegex("^[0-9a-f]{64}$");
        a.Should().NotBe(AssinaturaAutomais.GerarSegredo());
    }
}
