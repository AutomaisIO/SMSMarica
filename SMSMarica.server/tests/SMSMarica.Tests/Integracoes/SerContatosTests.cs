using FluentAssertions;
using SMSMarica.Core.Integracoes.SerWeb;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// A aba Editar do SER e os três telefones (docs/ser.md §10).
///
/// <para><b>O que estes testes protegem:</b> dois dos três telefones não têm <c>id</c>, só
/// <c>name</c> posicional (<c>form0:j_id173</c> residencial e <c>form0:j_id178</c> WhatsApp,
/// medidos em 10/08/2026). Resolver por posição gravaria no campo errado quando a SES-RJ
/// recompilar — e o SER responderia "salvo com sucesso" do mesmo jeito. Por isso o HTML abaixo
/// usa números DIFERENTES dos de produção e os embaralha em relação à ordem visual.</para>
/// </summary>
public class SerContatosTests
{
    /// <summary>
    /// Reproduz o essencial da tela: rótulo dentro do mesmo elemento do input, campos travados
    /// (identidade do paciente) e os telefones fora de ordem em relação aos ids.
    /// </summary>
    private const string AbaEditar = """
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-editar.seam">
          <input type="hidden" name="form0" value="form0" />
          <input type="hidden" name="javax.faces.ViewState" value="j_id9" />

          <div><label>Nome<span class="required">*</span></label>
               <input name="form0:nome" value="FULANA DE TAL" disabled="disabled" /></div>
          <div><label>CPF</label>
               <input name="form0:cpf" value="000.000.000-00" disabled="disabled" /></div>

          <div><label>Telefone Residencial</label>
               <input name="form0:j_id901" value="(21) 3333-0000" /></div>
          <div><label>Telefone WhatsApp<span class="required">*</span></label>
               <input name="form0:j_id777" value="(21) 97777-1111" /></div>
          <div><label>Telefone de Contato<span class="required">*</span></label>
               <input name="form0:telefoneContato" value="(21) 98888-2222" /></div>

          <a title="Gravar" id="form0:j_id950"
             onclick="A4J.AJAX.Submit('form0',event,{'similarityGroupingId':'form0:j_id950'})">Gravar</a>
        </form>
        <form id="j_id999">
          <a title="Gravar" id="j_id999:j_id1000">Gravar</a>
        </form>
        </body></html>
        """;

    [Fact]
    public void CampoPorRotulo_resolve_cada_telefone_pelo_seu_rotulo_e_nao_pela_ordem()
    {
        var doc = SerHtmlParser.Documento(AbaEditar);

        SerHtmlParser.CampoPorRotulo(doc, "form0", "Telefone Residencial")
            .Should().Be(("form0:j_id901", "(21) 3333-0000"));
        SerHtmlParser.CampoPorRotulo(doc, "form0", "Telefone WhatsApp")
            .Should().Be(("form0:j_id777", "(21) 97777-1111"));
        SerHtmlParser.CampoPorRotulo(doc, "form0", "Telefone de Contato")
            .Should().Be(("form0:telefoneContato", "(21) 98888-2222"));
    }

    [Fact]
    public void CampoPorRotulo_tolera_asterisco_de_obrigatorio_dois_pontos_e_caixa()
    {
        var doc = SerHtmlParser.Documento(AbaEditar);

        // O SER escreve o rótulo com <span class="required">*</span> e às vezes com ":".
        SerHtmlParser.CampoPorRotulo(doc, "form0", "telefone whatsapp:")
            .Should().NotBeNull().And.Be(("form0:j_id777", "(21) 97777-1111"));
    }

    [Fact]
    public void CampoPorRotulo_ignora_campo_travado()
    {
        // Identidade do paciente vem `disabled` — o navegador não a envia, e nós também não.
        // Devolver o campo aqui abriria caminho para um POST que tentasse reescrever o nome.
        SerHtmlParser.CampoPorRotulo(SerHtmlParser.Documento(AbaEditar), "form0", "Nome")
            .Should().BeNull();
    }

    [Fact]
    public void CampoPorRotulo_devolve_null_quando_o_rotulo_nao_existe()
    {
        // Falhar explícito é o que faz o serviço abortar ANTES de postar; chutar um campo faria
        // o SER gravar em outro lugar e responder sucesso.
        SerHtmlParser.CampoPorRotulo(SerHtmlParser.Documento(AbaEditar), "form0", "Telefone Comercial")
            .Should().BeNull();
    }

    [Fact]
    public void BotaoGravar_pega_o_do_form0_e_nao_o_de_um_modal()
    {
        // A tela tem outros "Gravar" (modais de cancelamento, FollowUP). Acionar o errado
        // dispara outra ação inteira.
        SerHtmlParser.BotaoGravar(SerHtmlParser.Documento(AbaEditar), "form0")
            .Should().Be("form0:j_id950");
    }

    [Fact]
    public void RegiaoDoBotao_sai_do_onclick_da_propria_pagina()
    {
        // A região decide QUAL pedaço da árvore JSF é processado. Chumbar `_viewRoot` não é
        // "mais abrangente" — é outra coisa.
        SerHtmlParser.RegiaoDoBotao(AbaEditar, "form0:j_id950").Should().Be("form0");
    }

    [Fact]
    public void CamposDoForm_nao_inclui_os_travados_entao_gravar_nao_zera_a_identidade()
    {
        var campos = SerHtmlParser.CamposDoForm(SerHtmlParser.Documento(AbaEditar), "form0", comoNavegador: true);

        campos.Should().NotContainKey("form0:nome");
        campos.Should().NotContainKey("form0:cpf");
        campos.Should().ContainKey("form0:j_id777");
    }

    [Fact]
    public void ItemEditar_acha_a_acao_da_linha_certa()
    {
        const string grade = """
            <html><body>
              <div id="form0:listagem:0:j_id150_menu">
                <a id="form0:listagem:0:j_id157">Visualizar</a>
                <a id="form0:listagem:0:j_id160">Editar</a>
              </div>
              <div id="form0:listagem:1:j_id150_menu">
                <a id="form0:listagem:1:j_id160">Editar</a>
              </div>
            </body></html>
            """;

        var doc = SerHtmlParser.Documento(grade);
        SerHtmlParser.ItemEditar(doc, 0).Should().Be("form0:listagem:0:j_id160");
        SerHtmlParser.ItemEditar(doc, 1).Should().Be("form0:listagem:1:j_id160");
    }

    [Fact]
    public void ItemEditar_devolve_null_em_situacao_terminal()
    {
        // Medido na 2749938 (Cancelada): o menu só tem Visualizar, Histórico e FollowUP.
        const string cancelada = """
            <html><body>
              <div id="form0:listagem:0:j_id150_menu">
                <a id="form0:listagem:0:j_id157">Visualizar</a>
                <a id="form0:listagem:0:j_id165">Historico da Solicitação</a>
                <a id="form0:listagem:0:j_id169">Registrar FollowUP</a>
              </div>
            </body></html>
            """;

        SerHtmlParser.ItemEditar(SerHtmlParser.Documento(cancelada), 0).Should().BeNull();
    }
}
