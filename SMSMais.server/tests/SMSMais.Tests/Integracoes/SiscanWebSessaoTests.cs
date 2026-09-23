using FluentAssertions;
using SMSMais.Core.Integracoes.SiscanWeb;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Parser e trava do SISCAN. São testes de UNIDADE sobre HTML fixo: o contrato de rede (login,
/// navegação por menu, criação de requisição) só existe contra a produção federal, com paciente
/// real — isso se faz à mão, no laboratório <c>Automais.SISCAN</c>, com autorização por ação.
///
/// <para>O que está fixado aqui é justamente o que custou medição e falha em silêncio quando
/// alguém "simplifica": o que o navegador posta e o que não posta, e o fato de a resposta A4J
/// não ser a tela.</para>
/// </summary>
public class SiscanWebSessaoTests
{
    /// <summary>
    /// Campo `disabled` não se reposta. No SISCAN metade da identidade do paciente vem assim
    /// (derivada do CADSUS) — repostar é, no melhor caso, ruído; no pior, tentar sobrescrever
    /// dado que o servidor manda.
    /// </summary>
    [Fact]
    public void Campos_ignora_disabled_e_botoes()
    {
        const string html = """
            <html><body><form id="frm" action="/x.jsf">
              <input name="frm:cartaoSUS" value="700000000000000" />
              <input name="frm:nome" value="FULANA" disabled="disabled" />
              <input type="submit" name="frm:btSalvar" value="Salvar" />
            </form></body></html>
            """;

        var campos = SiscanHtml.CamposDoForm(SiscanHtml.Documento(html), "frm");

        campos.Should().ContainKey("frm:cartaoSUS");
        campos.Should().NotContainKey("frm:nome");
        campos.Should().NotContainKey("frm:btSalvar");
    }

    /// <summary>
    /// Radio desmarcado não vai no POST — e é por isso que um valor empurrado só por A4J precisa
    /// ser reenviado à mão no submit completo. Foi o que fez o Avançar responder "O campo Tipo de
    /// Exame deve ser informado" com o radio aparecendo marcado na resposta (22/09/2026).
    /// </summary>
    [Fact]
    public void Campos_ignora_radio_desmarcado_e_leva_o_marcado()
    {
        const string html = """
            <html><body><form id="frm" action="/x.jsf">
              <input type="radio" name="frm:tipoExame" value="02" />
              <input type="radio" name="frm:tipoExame" value="01" checked="checked" />
            </form></body></html>
            """;

        var campos = SiscanHtml.CamposDoForm(SiscanHtml.Documento(html), "frm");

        campos["frm:tipoExame"].Should().Be("01");
    }

    /// <summary>
    /// A resposta A4J NÃO é a tela: é só o que está em `Ajax-Update-Ids`. Aplicá-la sobre o
    /// documento é o que o RichFaces faz no navegador — sem isso, quem lê a resposta crua não acha
    /// o form e conclui, errado, que a ação falhou.
    /// </summary>
    [Fact]
    public void A4J_aplica_so_as_regioes_citadas_e_troca_o_viewstate()
    {
        const string tela = """
            <html><body><form id="frm" action="/x.jsf">
              <div id="frm:painel"><span>antes</span></div>
              <div id="frm:intocado"><span>fica</span></div>
              <input type="hidden" name="javax.faces.ViewState" value="v1" />
            </form></body></html>
            """;
        const string parcial = """
            <html><body>
              <div id="frm:painel"><span>depois</span></div>
              <div id="frm:intocado"><span>lixo que não foi pedido</span></div>
              <meta name="Ajax-Update-Ids" content="frm:painel" />
              <span id="ajax-view-state">
                <input type="hidden" name="javax.faces.ViewState" value="v2" />
              </span>
            </body></html>
            """;

        var doc = SiscanHtml.Documento(tela);
        SiscanHtml.AplicarA4J(doc, SiscanHtml.Documento(parcial));

        doc.GetElementById("frm:painel")!.TextContent.Trim().Should().Be("depois");
        doc.GetElementById("frm:intocado")!.TextContent.Trim().Should().Be("fica");
        SiscanHtml.ViewState(doc).Should().Be("v2");
    }

    /// <summary>
    /// O par disparador do A4J é lido do JavaScript da própria página. Fixar `frm:j_idNN` no
    /// código quebra quando o DATASUS recompila — e pode acertar o campo errado, que é pior.
    /// </summary>
    [Fact]
    public void Parametros_a4j_saem_do_javascript_da_pagina()
    {
        const string html = """
            <html><body><form id="frm" action="/x.jsf">
              <input name="frm:cartaoSUS"
                     onblur="A4J.AJAX.Submit('frm',event,{'similarityGroupingId':'frm:j_id51','parameters':{'frm:j_id51':'frm:j_id51','ajaxSingle':'frm:cartaoSUS'} } )" />
            </form></body></html>
            """;

        var doc = SiscanHtml.Documento(html);
        var parametros = SiscanHtml.ParametrosA4JDoElemento(doc.QuerySelector("[name='frm:cartaoSUS']"));

        parametros.Should().Contain("ajaxSingle", "frm:cartaoSUS");
        parametros.Should().Contain("frm:j_id51", "frm:j_id51");
    }

    /// <summary>
    /// A trava pega o Salvar mesmo quando o id não entrega nada — o verbo mora no texto do
    /// elemento, como no SER.
    /// </summary>
    [Fact]
    public void Trava_recusa_salvar_com_id_opaco()
    {
        const string html = """
            <html><body><form id="frm" action="/x.jsf">
              <a id="frm:j_id999">Salvar</a>
            </form></body></html>
            """;

        var doc = SiscanHtml.Documento(html);
        var extras = new Dictionary<string, string> { ["frm:j_id999"] = "frm:j_id999" };

        var acao = () => SiscanWebSessao.GarantirLeitura(extras, doc, navegacaoLiberada: null);

        acao.Should().Throw<EscritaNoSiscanBloqueadaException>();
    }

    /// <summary>
    /// "Novo Exame" só renderiza a tela do assistente — nada é gravado antes do Salvar da etapa 2.
    /// Por isso ele é liberado <b>nominalmente</b>, e não afrouxando o verbo "novo" para todo mundo.
    /// </summary>
    [Fact]
    public void Trava_libera_novo_exame_por_nome_mas_nao_o_salvar()
    {
        const string html = """
            <html><body><form id="frm" action="/x.jsf">
              <a id="frm:botaoNovoExame">Novo Exame</a>
              <a id="frm:btSalvar">Salvar</a>
            </form></body></html>
            """;

        var doc = SiscanHtml.Documento(html);
        var liberado = SiscanWebSessao.NavegacaoDaRequisicao;

        var novo = () => SiscanWebSessao.GarantirLeitura(
            new Dictionary<string, string> { ["frm:botaoNovoExame"] = "frm:botaoNovoExame" }, doc, liberado);
        var salvar = () => SiscanWebSessao.GarantirLeitura(
            new Dictionary<string, string> { ["frm:btSalvar"] = "frm:btSalvar" }, doc, liberado);

        novo.Should().NotThrow();
        salvar.Should().Throw<EscritaNoSiscanBloqueadaException>();
    }

    /// <summary>O item de menu é achado pelo rótulo, e o id vem sem o sufixo `:anchor`.</summary>
    [Fact]
    public void Item_de_menu_por_rotulo()
    {
        const string html = """
            <html><body><form id="j_id32">
              <span class="rich-menu-item-label" id="j_id32:j_id64:anchor">GERENCIAR EXAME</span>
            </form></body></html>
            """;

        var doc = SiscanHtml.Documento(html);

        SiscanHtml.ItemDeMenu(doc, "Gerenciar Exame").Should().Be("j_id32:j_id64");
        SiscanHtml.FormDoItemDeMenu(doc, "j_id32:j_id64").Should().Be("j_id32");
        SiscanHtml.ItemDeMenu(doc, "TELA QUE NÃO EXISTE").Should().BeNull();
    }
}
