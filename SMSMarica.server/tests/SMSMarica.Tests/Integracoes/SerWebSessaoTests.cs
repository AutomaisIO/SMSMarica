using AngleSharp.Html.Dom;
using FluentAssertions;
using SMSMarica.Core.Integracoes.SerWeb;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Testes do parser e da trava de somente-leitura do SER — ADR-0042.
///
/// <para>São testes de UNIDADE sobre HTML fixo. O contrato de rede (login, ativação de módulo,
/// paginação) não dá para testar aqui: exige a produção da SES-RJ e derruba a sessão do operador
/// humano, porque a sessão do SER é única. Esse teste é feito à mão, na bancada.</para>
/// </summary>
public class SerWebSessaoTests
{
    /// <summary>
    /// A trava recusa "Registrar FollowUP" — o caso que mais importa, porque o id é OPACO
    /// (<c>j_id169</c>) e o verbo só existe no texto do elemento. Uma blacklist por nome de
    /// parâmetro, sozinha, deixaria passar.
    /// </summary>
    [Fact]
    public void Trava_recusa_registrar_followup_mesmo_com_id_opaco()
    {
        const string html = """
            <html><body><form id="form0">
              <a id="form0:listagem:0:j_id169">Registrar FollowUP</a>
            </form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);
        var extras = new Dictionary<string, string>
        {
            ["form0:listagem:0:j_id169"] = "form0:listagem:0:j_id169",
        };

        var acao = () => SerWebSessao.GarantirLeitura(extras, doc);

        acao.Should().Throw<EscritaNoSerBloqueadaException>()
            .WithMessage("*Registrar FollowUP*");
    }

    [Theory]
    [InlineData("Cancelar")]
    [InlineData("Editar")]
    [InlineData("Gravar")]
    public void Trava_recusa_acoes_de_escrita_do_menu(string rotulo)
    {
        var html = $"""
            <html><body><form id="form0"><a id="form0:x:j_id999">{rotulo}</a></form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);
        var extras = new Dictionary<string, string> { ["form0:x:j_id999"] = "form0:x:j_id999" };

        var acao = () => SerWebSessao.GarantirLeitura(extras, doc);

        acao.Should().Throw<EscritaNoSerBloqueadaException>();
    }

    [Theory]
    [InlineData("Visualizar")]
    [InlineData("Historico da Solicitação")]
    public void Trava_permite_acoes_de_leitura(string rotulo)
    {
        var html = $"""
            <html><body><form id="form0"><a id="form0:x:j_id999">{rotulo}</a></form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);
        var extras = new Dictionary<string, string> { ["form0:x:j_id999"] = "form0:x:j_id999" };

        var acao = () => SerWebSessao.GarantirLeitura(extras, doc);

        acao.Should().NotThrow();
    }

    /// <summary>
    /// A home do SER tem vários forms e os ViewStates PODEM diferir. Pegar o primeiro do
    /// documento faz o JSF restaurar a view errada e a ação não roda — HTTP 200, sem erro.
    /// </summary>
    [Fact]
    public void ViewState_vem_de_dentro_do_form_pedido()
    {
        const string html = """
            <html><body>
              <form id="formMenu">
                <input type="hidden" name="javax.faces.ViewState" value="j_id10" />
              </form>
              <form id="j_id23">
                <input type="hidden" name="javax.faces.ViewState" value="j_id3" />
              </form>
            </body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);

        SerHtmlParser.ViewStateDoForm(doc, "j_id23").Should().Be("j_id3");
        SerHtmlParser.ViewStateDoForm(doc, "formMenu").Should().Be("j_id10");
    }

    /// <summary>
    /// Solicitações em Alta não oferecem histórico. O parser devolve <c>null</c> em vez de
    /// chutar um j_id fixo — chutar faz o SER responder página vazia SEM erro, e o chamador
    /// acredita ter lido o histórico de alguém.
    /// </summary>
    [Fact]
    public void ItemHistorico_devolve_null_quando_o_menu_nao_oferece()
    {
        const string html = """
            <html><body><form id="form0">
              <a id="form0:listagem:0:j_id157">Visualizar</a>
            </form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);

        SerHtmlParser.ItemHistorico(doc, 0).Should().BeNull();
    }

    [Fact]
    public void ItemHistorico_acha_pelo_texto_e_nao_por_j_id_fixo()
    {
        const string html = """
            <html><body><form id="form0">
              <a id="form0:listagem:2:j_id157">Visualizar</a>
              <a id="form0:listagem:2:j_id777">Historico da Solicitação</a>
            </form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);

        SerHtmlParser.ItemHistorico(doc, 2).Should().Be("form0:listagem:2:j_id777");
    }

    /// <summary>O redirect do histórico vem no CORPO, não no header.</summary>
    [Fact]
    public void RedirectNoCorpo_le_o_meta_location()
    {
        const string corpo = """
            <html><head>
              <meta name="Ajax-Response" content="redirect" />
              <meta name="Location" content="/ser/pages/x/solicitar-consulta-historico.seam?cid=16453" />
            </head></html>
            """;

        SerHtmlParser.RedirectNoCorpo(corpo)
            .Should().Be("/ser/pages/x/solicitar-consulta-historico.seam?cid=16453");
    }

    /// <summary>
    /// O tbody do histórico REPETE o cabeçalho e traz linhas-tooltip só com a observação.
    /// Só é evento de verdade quem tem data dd/MM/yyyy.
    /// </summary>
    [Fact]
    public void LerHistorico_ignora_cabecalho_repetido_e_linhas_tooltip()
    {
        const string html = """
            <html><body>
              <table id="form0:historicoList">
                <thead><tr>
                  <th>Histórico da Solicitação</th><th></th><th>Data</th><th>Evento</th>
                  <th>Estado Anterior</th><th>Estado Atual</th><th>Central regulação</th>
                  <th>Unidade Executora</th><th>Usuário</th><th>Lotacao Evento</th>
                  <th>IP</th><th>Observação</th>
                </tr></thead>
                <tbody>
                  <tr><td></td><td>Data</td><td>Evento</td><td>Estado Anterior</td>
                      <td>Estado Atual</td><td>Central regulação</td><td>Unidade Executora</td>
                      <td>Usuário</td><td>Lotacao Evento</td><td>IP</td><td>Observação</td></tr>
                  <tr><td></td><td>03/08/2022 12:56:36</td><td>Solicitar</td><td>Em fila</td>
                      <td>Em fila</td><td>AMBULATÓRIO ESTADUAL</td><td></td>
                      <td>patricia</td><td>Gestor</td><td>10.42.0.180</td><td></td></tr>
                  <tr><td>PREZADO GESTOR, FAVOR FAZER CONTATO</td></tr>
                  <tr><td></td><td>19/01/2024 11:53:40</td><td>FollowUP</td><td>Em fila</td>
                      <td>Em fila</td><td>AMBULATÓRIO ESTADUAL</td><td></td>
                      <td>Sergio</td><td>Operador</td><td>10.42.88.20</td><td>SEM CONTATO</td></tr>
                </tbody>
              </table>
            </body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);

        var historico = SerHtmlParser.LerHistorico(doc);

        historico.Eventos.Should().HaveCount(2);
        historico.Eventos[0].Evento.Should().Be("Solicitar");
        historico.Eventos[1].Evento.Should().Be("FollowUP");
        historico.Eventos[1].Ip.Should().Be("10.42.88.20");
    }

    /// <summary>Dados do paciente casam pelo &lt;label&gt; do mesmo &lt;td&gt;, não pelo id volátil.</summary>
    [Fact]
    public void LerHistorico_le_dados_do_paciente_pelo_label()
    {
        const string html = """
            <html><body><table><tr>
              <td><label>CPF</label><input type="text" value="300.715.937-72" readonly /></td>
              <td><label>Nome Mãe</label><input type="text" value="JALCIRA" readonly /></td>
              <td><label>Telefone WhatsApp</label><input type="text" value="(21) 96715-6518" readonly /></td>
            </tr></table></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);

        var historico = SerHtmlParser.LerHistorico(doc);

        historico.Paciente["CPF"].Should().Be("300.715.937-72");
        historico.Paciente["Nome Mãe"].Should().Be("JALCIRA");
        historico.Paciente["Telefone WhatsApp"].Should().Be("(21) 96715-6518");
    }

    [Fact]
    public void Situacao_traduz_texto_da_grade_para_enum()
    {
        SerCodigos.DoTextoSituacao("Em fila").Should().Be(Data.Entities.Ser.SituacaoSer.EmFila);
        SerCodigos.DoTextoSituacao("Chegada Não Confirmada")
            .Should().Be(Data.Entities.Ser.SituacaoSer.ChegadaNaoConfirmada);
        SerCodigos.DoTextoSituacao("desconhecido").Should().BeNull();
    }
}
