using FluentAssertions;
using SMSMais.Core.Integracoes.SerWeb;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Qual é o form de escolha de módulo na home do SER.
///
/// <para>O caso que motivou estes testes, medido em 28/08/2026: quando há aviso pendente, o SER
/// renderiza a tabela de avisos ("Marcar Lida") ANTES do painel de módulos — e ela também é um
/// <c>&lt;form id="j_idNN" action="/ser/home"&gt;</c>. Escolher "o primeiro" mirava o form errado; o
/// POST era aceito, não fazia nada, e a falha aparecia como "o SER não redirecionou", jogando a
/// investigação no protocolo, que estava intacto. O ancoradouro certo é o <c>goModulo</c>, que a
/// própria página declara.</para>
///
/// <para>Os recortes abaixo são do HTML real capturado nos dois estados.</para>
/// </summary>
public class SerFormDeModuloTests
{
    /// <summary>Home com aviso pendente: o form de avisos vem PRIMEIRO e não há módulos.</summary>
    private const string HomeComAvisoPendente = """
        <html><body>
          <form id="formMenu" action="/ser/home"><input name="formMenu" value="formMenu" /></form>
          <form id="j_id49" name="j_id49" method="post" action="/ser/home">
            <input type="hidden" name="j_id49" value="j_id49" />
            <table class="rich-table" id="j_id50">
              <thead><tr><th>Prioridade</th><th>Mensagem</th><th>Ação</th></tr></thead>
              <tbody><tr>
                <td>Alta</td><td>À Unidade Executante Ambulatorial: a Chave de Autorização…</td>
                <td><input class="botao" name="j_id50:0:j_id59" type="submit" value="Marcar Lida"/></td>
              </tr></tbody>
            </table>
            <input name="javax.faces.ViewState" value="j_id3" />
          </form>
        </body></html>
        """;

    /// <summary>Depois de marcar o aviso como lido: o painel de módulos aparece.</summary>
    private const string HomeComModulos = """
        <html><body>
          <form id="formMenu" action="/ser/home"><input name="formMenu" value="formMenu" /></form>
          <div id="painel">
            <form id="j_id23" name="j_id23" method="post" action="/ser/home">
              <input type="hidden" name="j_id23" value="j_id23" />
              <script id="j_id23:goModulo" type="text/javascript">//<![CDATA[
                goModulo=function(param1){A4J.AJAX.Submit('j_id23',null,{'similarityGroupingId':'j_id23:goModulo','actionUrl':'/ser/home','parameters':{'j_id23:goModulo':'j_id23:goModulo','param1':param1} } )};
              //]]></script>
              <div class="module-card" data-module="ambulatorial" onclick="goModulo('ambulatorial')">
                <h3 class="module-title">Ambulatório</h3>
              </div>
              <div class="module-card" data-module="internacao" onclick="goModulo('internacao')">
                <h3 class="module-title">Internação</h3>
              </div>
              <input name="javax.faces.ViewState" value="j_id3" />
            </form>
          </div>
        </body></html>
        """;

    [Fact]
    public void Acha_o_form_pelo_goModulo_que_a_pagina_declara()
    {
        SerHtmlParser.FormDeModulo(HomeComModulos).Should().Be("j_id23");
    }

    /// <summary>
    /// O caso do incidente: sem módulos, é melhor não achar nada do que achar o form de avisos.
    /// Devolver o alvo errado fazia o POST "funcionar" e a falha mentir sobre a causa.
    /// </summary>
    [Fact]
    public void Nao_confunde_a_tabela_de_avisos_com_o_painel_de_modulos()
    {
        SerHtmlParser.FormDeModulo(HomeComAvisoPendente).Should().BeNull();
        SerHtmlParser.TemAvisoPendente(HomeComAvisoPendente).Should().BeTrue(
            "é o que permite dizer ao operador para ler o aviso, em vez de acusar o layout");
    }

    /// <summary>
    /// A home LIBERADA não pode ser confundida com "tem aviso": senão a mensagem de ajuda passaria
    /// a aparecer em qualquer falha futura, mandando o operador procurar um aviso que não existe.
    /// </summary>
    [Fact]
    public void Home_liberada_nao_reporta_aviso_pendente()
    {
        SerHtmlParser.TemAvisoPendente(HomeComModulos).Should().BeFalse();
    }

    /// <summary>
    /// O id do form é posicional e muda quando a SES-RJ recompila — foi de j_id49 para j_id23 no
    /// mesmo dia. O que não pode é o parser depender desse número.
    /// </summary>
    [Fact]
    public void Sobrevive_a_renumeracao_dos_j_id()
    {
        SerHtmlParser.FormDeModulo(HomeComModulos.Replace("j_id23", "j_id777")).Should().Be("j_id777");
    }

    [Fact]
    public void Home_sem_nada_reconhecivel_devolve_null()
    {
        SerHtmlParser.FormDeModulo("<html><body><form id=\"outro\"></form></body></html>")
            .Should().BeNull();
    }
}
