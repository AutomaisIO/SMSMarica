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

    /// <summary>
    /// REGRESSÃO da carga inicial de 06/08/2026: a tela de HISTÓRICO também tem
    /// &lt;form id="form0"&gt; (as abas Pesquisar/Editar/Historico vivem nele), mas NÃO tem o
    /// botão Pesquisar. Tratá-la como "página de formulário" fez o 1º histórico ser lido e os
    /// 10.501 seguintes falharem. O discriminador é o botão, não o form.
    /// </summary>
    [Fact]
    public void Tela_de_historico_nao_tem_botao_pesquisar()
    {
        const string historico = """
            <html><body><form id="form0">
              <td id="form0:pesquisar_cell"><span id="form0:pesquisar_lbl">Pesquisar</span></td>
              <table id="form0:historicoList"></table>
            </form></body></html>
            """;
        const string pesquisa = """
            <html><body><form id="form0">
              <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
            </form></body></html>
            """;

        SerHtmlParser.BotaoPesquisar((IHtmlDocument)SerHtmlParser.Documento(historico))
            .Should().BeNull("a tela de histórico não oferece o botão de pesquisa");
        SerHtmlParser.BotaoPesquisar((IHtmlDocument)SerHtmlParser.Documento(pesquisa))
            .Should().Be("form0:j_id96");
    }

    [Fact]
    public void Situacao_traduz_texto_da_grade_para_enum()
    {
        SerCodigos.DoTextoSituacao("Em fila").Should().Be(Data.Entities.Ser.SituacaoSer.EmFila);
        SerCodigos.DoTextoSituacao("Chegada Não Confirmada")
            .Should().Be(Data.Entities.Ser.SituacaoSer.ChegadaNaoConfirmada);
        SerCodigos.DoTextoSituacao("desconhecido").Should().BeNull();
    }

    // ------------------------------------------------------------------ tela de export
    // Fixtures reproduzem a ESTRUTURA das capturas de 06/08/2026 (capturas/ é gitignored por
    // conter PII de paciente) — mesmos ids, mesmas classes, mesmo texto de aviso.

    /// <summary>
    /// O aviso de corte é o que sustenta a afirmação de cobertura: sem ele, o lote é completo.
    /// Se a SES-RJ mudar essa frase e a detecção parar de casar, o varredor passa a tratar lote
    /// cortado como completo — e volta a perder registro em silêncio.
    /// </summary>
    [Fact]
    public void Aviso_de_limite_e_reconhecido_na_tela_de_historico()
    {
        const string html = """
            <html><body><form id="form0" action="/ser/pages/historico/x.seam">
              <div id="form0:divMensagens">
                <ul id="form0:messages" class="message">
                  <li style="font-weight: bold; color: blue;">
                    Consulta muito ampla, retorno limitado em 500 resultados.
                    &Eacute; recomendado restringir a consulta.
                  </li>
                </ul>
              </div>
            </form></body></html>
            """;

        SerHtmlParser.AvisoDeLimite((IHtmlDocument)SerHtmlParser.Documento(html))
            .Should().Contain("retorno limitado em 500");
    }

    [Fact]
    public void Sem_aviso_o_lote_e_considerado_completo()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:divMensagens"><ul id="form0:messages"></ul></div>
            </form></body></html>
            """;

        SerHtmlParser.AvisoDeLimite((IHtmlDocument)SerHtmlParser.Documento(html)).Should().BeNull();
    }

    [Fact]
    public void Link_exportar_e_achado_pelo_title()
    {
        const string html = """
            <html><body><form id="form0">
              <a id="form0:btnExport" href="#" title="Exportar para Excel"
                 onclick="if(typeof jsfcljs == 'function'){jsfcljs(document.getElementById('form0'),{'form0:btnExport':'form0:btnExport'},'');}return false">
                 <span>Exportar</span></a>
            </form></body></html>
            """;

        SerHtmlParser.BotaoExportar((IHtmlDocument)SerHtmlParser.Documento(html))
            .Should().Be("form0:btnExport");
    }

    /// <summary>
    /// O combo de Situação da tela de Histórico se chama <c>form0:j_id57</c> — e <c>j_id</c> é
    /// posicional. Localizar pelo conteúdo (quem oferece <c>EM_FILA</c>) sobrevive à recompilação
    /// da página; amarrar no id faria a consulta sair por outro campo, devolvendo outra listagem
    /// sem erro nenhum.
    /// </summary>
    [Fact]
    public void Combo_de_situacao_e_achado_pela_opcao_e_nao_pelo_j_id()
    {
        const string html = """
            <html><body><form id="form0">
              <select name="form0:tipo"><option value="CONSULTA">CONSULTA</option></select>
              <select name="form0:j_id57">
                <option value="AGENDADA">Agendada</option>
                <option value="EM_FILA">Em fila</option>
              </select>
            </form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);

        SerHtmlParser.SelectComOpcao(doc, "form0", "EM_FILA").Should().Be("form0:j_id57");
        SerHtmlParser.SelectComOpcao(doc, "form0", "CONSULTA").Should().Be("form0:tipo");
        SerHtmlParser.SelectComOpcao(doc, "form0", "ALTA").Should().BeNull();
    }

    /// <summary>
    /// A trava é agressiva de propósito, mas não pode barrar a própria leitura: "Exportar para
    /// Excel" não é escrita. Se um dia o regex de escrita crescer e pegar isso, a varredura inteira
    /// para — este teste é o alarme.
    /// </summary>
    [Fact]
    public void Trava_permite_o_link_exportar()
    {
        const string html = """
            <html><body><form id="form0">
              <a id="form0:btnExport" title="Exportar para Excel"><span>Exportar</span></a>
            </form></body></html>
            """;
        var doc = (IHtmlDocument)SerHtmlParser.Documento(html);
        var extras = new Dictionary<string, string> { ["form0:btnExport"] = "form0:btnExport" };

        var acao = () => SerWebSessao.GarantirLeitura(extras, doc);

        acao.Should().NotThrow();
    }

    /// <summary>
    /// REGRESSÃO de 10/08/2026 — a varredura diária morreu às 02:30 em menos de um segundo, e
    /// todo disparo manual seguinte também, até o processo reiniciar.
    ///
    /// <para><b>Causa:</b> sessão morta no SER não devolve 401 nem redirect — devolve <b>HTTP 200
    /// com a tela de login no corpo</b>. Como <c>Logado</c> só era marcado no login e nunca
    /// desmarcado, a sessão em memória era considerada boa para sempre: o GET voltava a tela de
    /// login, quem chamou não achava o botão Pesquisar, e a rodada inteira morria sem nunca tentar
    /// reautenticar.</para>
    ///
    /// <para>Este teste guarda o reconhecimento. Se ele parar de reconhecer a tela de login, a
    /// reautenticação automática deixa de disparar e o modo de falha volta inteiro.</para>
    /// </summary>
    [Fact]
    public void Tela_de_login_e_reconhecida_como_sessao_morta()
    {
        // Estrutura do /ser/login real: o SER devolve isto no lugar da página pedida.
        const string login = """
            <html><body><form id="login" action="/ser/login;jsessionid=ABC">
              <input id="login:username" name="login:username" type="text" />
              <input id="login:password" name="login:password" type="password" />
            </form></body></html>
            """;

        SerWebSessao.EhTelaDeLogin(login).Should().BeTrue();
    }

    /// <summary>
    /// O contrário importa igual: confundir a tela de pesquisa com a de login faria o motor
    /// relogar em loop a cada requisição bem-sucedida.
    /// </summary>
    [Fact]
    public void Tela_normal_do_ser_nao_e_confundida_com_login()
    {
        const string pesquisa = """
            <html><body><form id="form0" action="/ser/pages/x.seam">
              <input id="form0:login_usuario_exibicao" name="form0:usuario" value="operador" />
              <a id="form0:pesquisar"><span>Pesquisar</span></a>
            </form></body></html>
            """;

        SerWebSessao.EhTelaDeLogin(pesquisa).Should().BeFalse(
            "campo com 'login' no nome não é a tela de login");
    }
}
