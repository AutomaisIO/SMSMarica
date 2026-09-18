using SMSMais.Core.Integracoes.SisregWeb.Comum;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>Regras de grupo↔item do código do SISREG — a régua compartilhada por Ofertas e Estratégias.</summary>
public class FamiliaProcedimentoSisregTests
{
    [Theory]
    [InlineData("0229000", true)]
    [InlineData("0229010", false)]
    [InlineData("229000", false)]
    [InlineData("", false)]
    public void Codigo_de_grupo_termina_em_000_e_tem_7_digitos(string codigo, bool esperado) =>
        FamiliaProcedimentoSisreg.EhCodigoDeGrupo(codigo).Should().Be(esperado);

    [Theory]
    [InlineData("0229010", "0229000")]
    [InlineData("0229000", "0229000")]
    [InlineData("ABC0000", null)]
    [InlineData("12345", null)]
    public void Grupo_do_codigo_e_o_prefixo_mais_000(string codigo, string? esperado) =>
        FamiliaProcedimentoSisreg.GrupoDoCodigo(codigo).Should().Be(esperado);

    [Fact]
    public void Recorte_de_codigo_distingue_grupo_item_e_nada()
    {
        FamiliaProcedimentoSisreg.RecorteDeCodigo("0229000").Should().Be(("0229", true));
        FamiliaProcedimentoSisreg.RecorteDeCodigo("0229010").Should().Be(("0229", false));
        FamiliaProcedimentoSisreg.RecorteDeCodigo(null).Should().BeNull();
        FamiliaProcedimentoSisreg.RecorteDeCodigo("x").Should().BeNull();
    }
}
