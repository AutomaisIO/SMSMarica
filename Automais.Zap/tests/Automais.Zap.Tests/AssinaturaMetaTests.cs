using System.Text;
using Automais.Zap.Core.Meta;
using FluentAssertions;

namespace Automais.Zap.Tests;

public sealed class AssinaturaMetaTests
{
    private const string Segredo = "segredo-de-teste";

    [Fact]
    public void Calcular_produz_o_formato_que_a_Meta_manda()
    {
        var assinatura = AssinaturaMeta.Calcular(Segredo, "{}"u8);

        assinatura.Should().StartWith("sha256=");
        assinatura[7..].Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Confere_aceita_a_assinatura_do_proprio_corpo()
    {
        var corpo = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account"}""");
        var assinatura = AssinaturaMeta.Calcular(Segredo, corpo);

        AssinaturaMeta.Confere(Segredo, corpo, assinatura).Should().BeTrue();
    }

    [Fact]
    public void Confere_recusa_corpo_adulterado()
    {
        var original = Encoding.UTF8.GetBytes("""{"a":1}""");
        var adulterado = Encoding.UTF8.GetBytes("""{"a":2}""");
        var assinatura = AssinaturaMeta.Calcular(Segredo, original);

        AssinaturaMeta.Confere(Segredo, adulterado, assinatura).Should().BeFalse();
    }

    [Fact]
    public void Confere_recusa_segredo_errado()
    {
        var corpo = "{}"u8.ToArray();
        var assinatura = AssinaturaMeta.Calcular(Segredo, corpo);

        AssinaturaMeta.Confere("outro-segredo", corpo, assinatura).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sha256=")]
    [InlineData("naoehassinatura")]
    public void Confere_recusa_assinatura_ausente_ou_malformada(string? assinatura)
    {
        AssinaturaMeta.Confere(Segredo, "{}"u8, assinatura).Should().BeFalse();
    }
}
