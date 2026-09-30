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
    /// NEM TODO A4J RESPONDE PARCIAL — e confundir os dois derrubou a geração em produção
    /// (23/09/2026). O botão "Novo Exame" é um A4J que NAVEGA: responde a tela inteira, sem
    /// `Ajax-Update-Ids`.
    ///
    /// <para>Quem aplica o parcial cegamente não tem região nenhuma para substituir e devolve
    /// <b>a página velha intacta</b>. O fluxo seguia na tela de Gerenciar Exame achando estar no
    /// assistente — e, como aquela tela também tem um `frm:cartaoSUS` (o filtro de busca), o erro
    /// só aparecia lá adiante, como "não consegui resolver o CNS", para TODOS os pacientes.</para>
    /// </summary>
    [Fact]
    public void Resposta_a4j_de_navegacao_nao_e_parcial()
    {
        const string telaInteira = """
            <html><body><form id="frm" action="/visao/exame/novoExame.jsf">
              <input name="frm:cartaoSUS" /><input type="radio" name="frm:tipoExame" value="01" />
            </form></body></html>
            """;
        const string parcialDeVerdade = """
            <html><body>
              <div id="frm:pnlExame">novo conteudo</div>
              <meta name="Ajax-Update-Ids" content="frm:pnlExame" />
            </body></html>
            """;

        SiscanHtml.EhRespostaParcial(SiscanHtml.Documento(telaInteira)).Should().BeFalse();
        SiscanHtml.EhRespostaParcial(SiscanHtml.Documento(parcialDeVerdade)).Should().BeTrue();
    }

    /// <summary>
    /// A consequência direta do caso acima: aplicar um "parcial" sem regiões devolve a base
    /// intacta. É por isso que quem chama precisa perguntar ANTES se a resposta é parcial.
    /// </summary>
    [Fact]
    public void Aplicar_resposta_sem_regioes_devolve_a_base_intacta()
    {
        const string baseHtml = """
            <html><body><form id="frm"><div id="frm:painel">tela VELHA</div></form></body></html>
            """;
        const string telaNova = """
            <html><body><form id="frm"><div id="frm:painel">tela NOVA</div></form></body></html>
            """;

        var doc = SiscanHtml.Documento(baseHtml);
        SiscanHtml.AplicarA4J(doc, SiscanHtml.Documento(telaNova));

        doc.GetElementById("frm:painel")!.TextContent.Should().Be("tela VELHA");
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

    /// <summary>
    /// Ticket #137: é por aqui que se sabe que a sessão caiu por ociosidade — o SISCAN responde
    /// HTTP 200 com o formulário de login, nunca 401. Tela logada não pode ser confundida com ele.
    /// </summary>
    [Theory]
    [InlineData("""<html><body><form id="formLogin" action="/login.jsf"><input name="email"/></form></body></html>""", true)]
    [InlineData("""<form name="formLogin" method="post"></form>""", true)]
    [InlineData("""<html><body><form id="j_id32"><span class="rich-menu-item-label">GERENCIAR EXAME</span></form></body></html>""", false)]
    public void Tela_de_login_denuncia_sessao_expirada(string html, bool esperado) =>
        SiscanWebSessao.EhTelaDeLogin(html).Should().Be(esperado);

    /// <summary>
    /// A frase que o SISCAN mostra acima de "Quando fez a última mamografia?" — recorte da tela real
    /// (captura de 23/09/2026), com a entidade HTML e a quebra de linha que vêm dela.
    /// </summary>
    [Fact]
    public void Ano_do_ultimo_exame_no_sus_sai_da_frase_da_tela()
    {
        var doc = SiscanHtml.Documento("""
            <form id="frm"><div id="frm:ultimaMamografia">
              <div class="grid-12-12 espacamento"><label class="form-lbl">
            O &uacute;ltimo exame realizado no SUS foi em 2022.</label></div>
              <fieldset><legend>QUANDO FEZ A &Uacute;LTIMA MAMOGRAFIA?</legend>
                <input id="frm:anoUltimaMamografia" name="frm:anoUltimaMamografia" value="" />
              </fieldset>
            </div></form>
            """);

        SiscanHtml.AnoDoUltimoExameNoSus(doc).Should().Be(2022);
    }

    /// <summary>
    /// Paciente sem exame anterior no SUS: a região abre sem a frase (captura
    /// <c>12-map-j_id105-01</c>). E ano fora da região não é este número.
    /// </summary>
    [Theory]
    [InlineData("""<div id="frm:ultimaMamografia"><fieldset><legend>QUANDO FEZ A ÚLTIMA MAMOGRAFIA?</legend><input name="frm:anoUltimaMamografia"/></fieldset></div>""")]
    [InlineData("""<p>O último exame realizado no SUS foi em 2022.</p><div id="frm:ultimaMamografia"></div>""")]
    [InlineData("""<form id="frm"></form>""")]
    public void Sem_a_frase_na_regiao_nao_ha_ano(string html) =>
        SiscanHtml.AnoDoUltimoExameNoSus(SiscanHtml.Documento(html)).Should().BeNull();

    /// <summary>
    /// O combo "Unidade Requisitante" — recorte da captura <c>07-tipo-exame.html</c>. O "Selecionar"
    /// fica de fora, e o CNES é separado do nome porque é ele a chave (o value é posicional).
    /// </summary>
    [Fact]
    public void Unidades_requisitantes_saem_do_combo_com_cnes_e_nome()
    {
        var doc = SiscanHtml.Documento("""
            <form id="frm"><select id="frm:unidadeSaude2" name="frm:unidadeSaude2" size="1">
              <option value="0" selected="selected">Selecionar</option>
              <option value="1">2266741 - AMBULATORIO PERICLES SIQUEIRA FERREIRA</option>
              <option value="8">6886973 - SECRETARIA MUNICIPAL DE SAUDE DE MARICA</option>
              <option value="35">3055779 - USF ELENIR UMBELINO DE MELLO ENFERMEIRA BILLU</option>
            </select></form>
            """);

        SiscanHtml.UnidadesRequisitantes(doc).Should().Equal(
            new SiscanHtml.UnidadeRequisitante("1", "2266741", "AMBULATORIO PERICLES SIQUEIRA FERREIRA"),
            new SiscanHtml.UnidadeRequisitante("8", "6886973", "SECRETARIA MUNICIPAL DE SAUDE DE MARICA"),
            new SiscanHtml.UnidadeRequisitante("35", "3055779", "USF ELENIR UMBELINO DE MELLO ENFERMEIRA BILLU"));
    }

    /// <summary>Antes do A4J do tipo de exame o combo só tem o "Selecionar": lista vazia.</summary>
    [Fact]
    public void Combo_de_unidades_ainda_vazio_nao_inventa_unidade() =>
        SiscanHtml.UnidadesRequisitantes(SiscanHtml.Documento(
            """<select name="frm:unidadeSaude2"><option value="0">Selecionar</option></select>"""))
            .Should().BeEmpty();

    /// <summary>
    /// Os campos da paciente que o SISCAN puxa do CADSUS e trava — recorte da captura
    /// <c>05-novo-exame.html</c>. Raça/Cor tem rótulo por <c>id</c>, sem <c>for</c>; Apelido é do
    /// CADSUS mas não é obrigatório, e fica de fora.
    /// </summary>
    [Fact]
    public void Campos_obrigatorios_do_cadsus_com_rotulo_e_vazio()
    {
        var doc = SiscanHtml.Documento("""
            <form id="frm">
              <div class="grid-6-12-alt"><label for="frm:apelido" class="form-lbl ">Apelido</label><input id="frm:apelido" type="text" name="frm:apelido" class="form-txt pessoaCadsus" /></div>
              <div class="grid-6-12-alt"><label for="frm:nomeMae" class="form-lbl">
            Nome da M&atilde;e
                <em class="form-req">*</em></label><input id="frm:nomeMae" type="text" name="frm:nomeMae" value="MAE EXEMPLO" class="form-txt pessoaCadsus" disabled="disabled" /></div>
              <div class="grid-6-12-alt"><label id="frm:lblRacaCor" class="form-lbl">
            Ra&ccedil;a/Cor:
                <em class="form-req">*</em></label><input id="frm:racaCor" type="text" name="frm:racaCor" class="form-txt pessoaCadsus" disabled="disabled" /></div>
            </form>
            """);

        SiscanHtml.CamposObrigatoriosDoCadsus(doc).Should().Equal(
            new SiscanHtml.CampoCadsus("frm:nomeMae", "Nome da Mãe", false),
            new SiscanHtml.CampoCadsus("frm:racaCor", "Raça/Cor", true));
    }

    [Theory]
    [InlineData("O campo Raça/Cor é obrigatório.", "Raça/Cor", true)]
    [InlineData("RACA COR NAO INFORMADA", "Raça/Cor", true)]
    [InlineData("O campo UF deve ser informado", "UF", true)]
    [InlineData("Informação insuficiente", "UF", false)]
    [InlineData("O campo Tipo de Exame deve ser informado", "Raça/Cor", false)]
    public void Mensagem_cita_rotulo_por_palavra_inteira(string mensagem, string rotulo, bool esperado) =>
        SiscanHtml.MencionaRotulo(mensagem, rotulo).Should().Be(esperado);

    /// <summary>A prévia mostrava "frm:anoMastectomia…" como pergunta; agora é legível.</summary>
    [Theory]
    [InlineData("frm:anoMastectomiaPoupadoraPeleDireita", "Ano — Mastectomia poupadora pele (direita)")]
    [InlineData("frm:anoRadioterapiaEsquerda", "Ano — Radioterapia (esquerda)")]
    [InlineData("frm:anoUltimaMamografia", null)]
    [InlineData("frm:prontuario", null)]
    public void Campo_de_ano_ganha_rotulo_legivel(string campo, string? esperado) =>
        SMSMais.Core.Integracoes.SiscanWeb.Requisicao.SiscanRequisicaoService.RotuloDeAno(campo)
            .Should().Be(esperado);
}
