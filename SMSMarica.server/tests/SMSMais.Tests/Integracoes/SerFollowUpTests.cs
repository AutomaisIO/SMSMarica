using FluentAssertions;
using SMSMarica.Core.Integracoes.SerWeb;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O modal de FollowUP (docs/ser.md §9), medido contra o SER real em 18/08/2026.
///
/// <para><b>O que estes testes protegem:</b> os três ids do modal são POSICIONAIS
/// (<c>j_id175</c>, <c>j_id175:j_id183</c>, <c>j_id175:j_id185</c>) e mudam quando a SES-RJ
/// recompila a página. Se alguém "simplificar" o parser chumbando um deles, o registro passa a
/// ir para o campo errado — ou para lugar nenhum — <b>sem erro</b>, que é como esse sistema
/// falha. Por isso o HTML dos testes usa ids DIFERENTES dos medidos em produção.</para>
/// </summary>
public class SerFollowUpTests
{
    /// <summary>A tela real tem vários forms; o do modal é só mais um no meio.</summary>
    private const string TelaComModal = """
        <html><body>
          <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
            <input type="hidden" name="form0" value="form0" />
            <input type="hidden" name="javax.faces.ViewState" value="j_id5" />
            <a title="Pesquisar" id="form0:btnPesquisar">Pesquisar</a>
          </form>
          <form id="j_id999" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
            <span>Paciente: FULANA DE TAL</span>
            <span>Observação:</span>
            <textarea name="j_id999:j_id1001"></textarea>
            <input type="hidden" name="j_id999" value="j_id999" />
            <input type="hidden" name="autoScroll" value="" />
            <input type="hidden" name="javax.faces.ViewState" value="j_id7" />
            <a title="Gravar" id="j_id999:j_id1003" name="j_id999:j_id1003">Gravar</a>
            <a title="Cancelar" id="j_id999:j_id1004" name="j_id999:j_id1004">Cancelar</a>
          </form>
        </body></html>
        """;

    [Fact]
    public void ModalDeObservacao_resolve_form_textarea_e_gravar_sem_chumbar_j_id()
    {
        var modal = SerHtmlParser.ModalDeObservacao(SerHtmlParser.Documento(TelaComModal));

        modal.Should().NotBeNull();
        modal!.FormId.Should().Be("j_id999");
        modal.CampoTexto.Should().Be("j_id999:j_id1001");
        modal.BotaoGravar.Should().Be("j_id999:j_id1003");
    }

    [Fact]
    public void ModalDeObservacao_usa_o_ViewState_de_DENTRO_do_form_do_modal()
    {
        var modal = SerHtmlParser.ModalDeObservacao(SerHtmlParser.Documento(TelaComModal));

        // O do form0 é "j_id5". Mandar ele faria o JSF restaurar a view errada e a ação não
        // rodaria — HTTP 200 e nada gravado (docs/ser.md §3.2).
        modal!.ViewState.Should().Be("j_id7");
    }

    [Fact]
    public void ModalDeObservacao_ignora_form_sem_textarea()
    {
        // Só o form0, que tem botão mas não tem textarea: não é o modal.
        const string semModal = """
            <html><body>
              <form id="form0" action="/x">
                <a title="Gravar" id="form0:j_id1">Gravar</a>
              </form>
            </body></html>
            """;

        SerHtmlParser.ModalDeObservacao(SerHtmlParser.Documento(semModal)).Should().BeNull();
    }

    [Fact]
    public void ItemFollowUp_acha_pelo_texto_e_tolera_hifen_e_caixa()
    {
        const string grade = """
            <html><body>
              <div id="form0:listagem:0:j_id150_menu">
                <a id="form0:listagem:0:j_id157">Visualizar</a>
                <a id="form0:listagem:0:j_id165">Historico da Solicitação</a>
                <a id="form0:listagem:0:j_id169">Registrar Follow-UP</a>
              </div>
            </body></html>
            """;

        SerHtmlParser.ItemFollowUp(SerHtmlParser.Documento(grade), 0)
            .Should().Be("form0:listagem:0:j_id169");
    }

    [Fact]
    public void ItemFollowUp_nao_confunde_a_linha_de_outra_solicitacao()
    {
        // Duas linhas na grade: pedir o FollowUP da linha 1 não pode devolver o da linha 0.
        // Errar aqui registra observação no prontuário do paciente errado.
        const string duasLinhas = """
            <html><body>
              <div id="form0:listagem:0:j_id150_menu">
                <a id="form0:listagem:0:j_id169">Registrar FollowUP</a>
              </div>
              <div id="form0:listagem:1:j_id150_menu">
                <a id="form0:listagem:1:j_id169">Registrar FollowUP</a>
              </div>
            </body></html>
            """;

        var doc = SerHtmlParser.Documento(duasLinhas);
        SerHtmlParser.ItemFollowUp(doc, 1).Should().Be("form0:listagem:1:j_id169");
        SerHtmlParser.ItemFollowUp(doc, 0).Should().Be("form0:listagem:0:j_id169");
    }

    [Fact]
    public void ItemFollowUp_devolve_null_quando_a_situacao_nao_oferece_a_acao()
    {
        // Alta não oferece ação nenhuma; devolver null é o que faz o motor falhar com mensagem,
        // em vez de postar um j_id chutado (o SER responderia 200 e página vazia).
        const string semFollowUp = """
            <html><body>
              <div id="form0:listagem:0:j_id150_menu">
                <a id="form0:listagem:0:j_id157">Visualizar</a>
              </div>
            </body></html>
            """;

        SerHtmlParser.ItemFollowUp(SerHtmlParser.Documento(semFollowUp), 0).Should().BeNull();
    }

    [Fact]
    public void MensagemDaTela_le_divMensagens_que_e_onde_sai_o_retorno_das_acoes()
    {
        // A confirmação do FollowUP e a recusa da criação saem em `divMensagens`, não em
        // `messages`. Ler só a segunda faria uma ação recusada parecer silenciosa.
        const string resposta = """
            <html><body>
              <div id="form0:divMensagens"><ul><li>FollowUp registrado!</li></ul></div>
            </body></html>
            """;

        SerHtmlParser.MensagemDaTela(SerHtmlParser.Documento(resposta))
            .Should().Contain("FollowUp registrado!");
    }
}
