using SMSMais.Core.Integracoes.SisregWeb.Chave;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Chave de confirmação na ficha do SISREG (<c>cons_marcados_reg</c>, <c>EXIBIR_FICHA</c>).
///
/// <para><b>HTML sintético com a estrutura real</b> das capturas do laboratório
/// (<c>ficha_*.html</c>, linhas 206-222). Valores inventados — a captura tem dado de paciente.</para>
/// </summary>
public class FichaChaveHtmlParserTests
{
    private static string Ficha(string rotulo = "Chave de Confirma&#231;&#227;o:", string valor = "<b>98626</b>") =>
        $"""
        <FORM NAME="formulario" METHOD="POST">
        <INPUT type="hidden" name="etapa" value="EXIBIR_FICHA"/>
        <div class="fichaAmbulatorial"><div id="fichaAmbulatorial">	<table class='table_listagem' width='700'>
        	<tr>
        		<td style='text-align: left;'><b>{rotulo}</b></td>
        	</tr>
        	<tr>
        		<td style='text-align: left; font-size: 180%;'>{valor}</td>
        	</tr>
        <tbody class="FichaCompleta">
        	<tr><td><b>Unidade Solicitante:</b></td><td><b>C&oacute;d. CNES:</b></td></tr>
        	<tr><td>UNIDADE DE SAUDE DA FAMILIA TESTE</td><td><b>2266946</b></td></tr>
        </tbody>
        """;

    [Fact]
    public void Le_a_chave_com_o_rotulo_em_entidade_numerica() =>
        Assert.Equal("98626", FichaChaveHtmlParser.Ler(Ficha()));

    [Fact]
    public void Le_a_chave_com_entidade_nomeada_e_com_acento_literal()
    {
        Assert.Equal("98626", FichaChaveHtmlParser.Ler(Ficha("Chave de Confirma&ccedil;&atilde;o:")));
        Assert.Equal("98626", FichaChaveHtmlParser.Ler(Ficha("Chave de Confirmação:")));
    }

    [Fact]
    public void Tolera_espaco_em_volta_do_valor() =>
        Assert.Equal("98626", FichaChaveHtmlParser.Ler(Ficha(valor: "<b> 98626 </b>")));

    /// <summary>O risco que o regex precisa evitar: célula da chave vazia e o próximo negrito
    /// numérico da ficha (o CNES) virando "chave".</summary>
    [Fact]
    public void Celula_vazia_nao_escorrega_para_o_proximo_campo()
    {
        Assert.Null(FichaChaveHtmlParser.Ler(Ficha(valor: "<b></b>")));
        Assert.Null(FichaChaveHtmlParser.Ler(Ficha(valor: "")));
    }

    [Fact]
    public void Pagina_sem_o_rotulo_devolve_nulo()
    {
        Assert.Null(FichaChaveHtmlParser.Ler("<html><body>SOLICITACOES INEXISTENTES!</body></html>"));
        Assert.Null(FichaChaveHtmlParser.Ler(""));
        Assert.Null(FichaChaveHtmlParser.Ler(null));
    }
}
