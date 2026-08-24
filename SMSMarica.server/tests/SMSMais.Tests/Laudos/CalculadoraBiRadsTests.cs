using SMSMais.Core.Laudos.BiRads;

namespace SMSMais.Tests.Laudos;

/// <summary>
/// Testes puros (sem banco) do motor BI-RADS — regra ACR 5ª ed. "achado mais
/// suspeito manda" (máximo de suspeição), não soma de pontos.
/// </summary>
public class CalculadoraBiRadsTests
{
    [Fact]
    public void Sem_contribuicoes_retorna_null()
    {
        CalculadoraBiRads.Sugerir([]).Should().BeNull();
        CalculadoraBiRads.Sugerir([null, "", "   "]).Should().BeNull();
    }

    [Fact]
    public void Pega_o_achado_mais_suspeito_e_nao_a_soma()
    {
        // 5 benignos + 1 suspeito 4C → resultado é 4C (máximo), não média/soma.
        var contribs = new[] { "2", "2", "1", "2", "2", "4C" };
        CalculadoraBiRads.Sugerir(contribs).Should().Be("4C");
    }

    [Theory]
    [InlineData(new[] { "1" }, "1")]
    [InlineData(new[] { "2", "1" }, "2")]
    [InlineData(new[] { "3", "2" }, "3")]
    [InlineData(new[] { "5", "2", "3" }, "5")]
    [InlineData(new[] { "6", "5" }, "6")]
    public void Retorna_a_categoria_de_maior_suspeicao(string[] contribs, string esperado)
    {
        CalculadoraBiRads.Sugerir(contribs).Should().Be(esperado);
    }

    [Theory]
    [InlineData(new[] { "4A", "4B" }, "4B")]
    [InlineData(new[] { "4B", "4C" }, "4C")]
    [InlineData(new[] { "4", "4A" }, "4A")]
    [InlineData(new[] { "4C", "4A", "4B" }, "4C")]
    public void Ordena_subcategorias_4A_4B_4C_corretamente(string[] contribs, string esperado)
    {
        CalculadoraBiRads.Sugerir(contribs).Should().Be(esperado);
    }

    [Fact]
    public void Incompleto_vence_quando_demais_achados_sao_benignos()
    {
        // "Mamas densas + recomenda US" sobre achados benignos → 0 (incompleto).
        CalculadoraBiRads.Sugerir(["2", "1", "0"]).Should().Be("0");
        CalculadoraBiRads.Sugerir(["0"]).Should().Be("0");
    }

    [Fact]
    public void Incompleto_nao_vence_diante_de_achado_suspeito()
    {
        // Diante de 3+, a suspeição domina o incompleto.
        CalculadoraBiRads.Sugerir(["0", "3"]).Should().Be("3");
        CalculadoraBiRads.Sugerir(["0", "4B", "2"]).Should().Be("4B");
        CalculadoraBiRads.Sugerir(["0", "5"]).Should().Be("5");
    }

    [Fact]
    public void Normaliza_caixa_e_espacos()
    {
        CalculadoraBiRads.Sugerir([" 4c ", "2"]).Should().Be("4C");
        CalculadoraBiRads.Normalizar("4a").Should().Be("4A");
        CalculadoraBiRads.Normalizar("  ").Should().BeNull();
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("4B", true)]
    [InlineData("4b", true)]
    [InlineData("6", true)]
    [InlineData("7", false)]
    [InlineData("4D", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Valida_categorias(string? categoria, bool esperado)
    {
        CalculadoraBiRads.EhCategoriaValida(categoria).Should().Be(esperado);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("4A")]
    [InlineData("5")]
    public void Conduta_existe_para_categorias_validas(string categoria)
    {
        CalculadoraBiRads.Conduta(categoria).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Conduta_vazia_para_categoria_invalida()
    {
        CalculadoraBiRads.Conduta("9").Should().BeEmpty();
    }
}
