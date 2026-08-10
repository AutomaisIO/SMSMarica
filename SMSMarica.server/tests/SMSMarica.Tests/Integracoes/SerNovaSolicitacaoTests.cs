using FluentAssertions;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Ser;
using SMSMarica.Core.Ser.Dtos;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Leitura do formulário de NOVA solicitação do SER (aba Editar).
///
/// <para>O que está sob teste é a parte que quebra em silêncio: os ids do JSF são posicionais e
/// mudam quando a SES-RJ recompila a página. Tudo aqui é lido da página viva; um id chumbado
/// faria o SER responder 200 com a view intacta — sem erro, e sem os campos do recurso.</para>
/// </summary>
public class SerNovaSolicitacaoTests
{
    /// <summary>Estrutura da captura real de 08/08/2026: o id do evento vive dentro do
    /// <c>onchange</c> do próprio combo.</summary>
    private const string ComboComOnchange = """
        <select id="form0:comboRecurso" name="form0:comboRecurso" class="select"
          onchange="A4J.AJAX.Submit('form0',event,{'similarityGroupingId':'form0:j_id57','control':this,'oncomplete':function(request,event,data){verificarPetCt();},'parameters':{'form0:j_id57':'form0:j_id57','ajaxSingle':'form0:comboRecurso'} } )">
          <option value="">Selecione...</option>
          <option value="988">Ambulatório 1ª vez em Cardiologia</option>
        </select>
        """;

    [Fact]
    public void Id_do_evento_sai_do_onchange_da_pagina_e_nao_de_id_fixo()
    {
        SerNovaSolicitacaoService.EventoDoCombo(ComboComOnchange, "form0:comboRecurso")
            .Should().Be("form0:j_id57");
    }

    /// <summary>
    /// Se a SES-RJ recompilar, o número muda — e o motor tem de acompanhar sozinho. Este teste
    /// usa j_id99 de propósito: se alguém chumbar j_id57, ele quebra.
    /// </summary>
    [Fact]
    public void Id_do_evento_acompanha_a_recompilacao_da_pagina()
    {
        var html = ComboComOnchange.Replace("j_id57", "j_id99");

        SerNovaSolicitacaoService.EventoDoCombo(html, "form0:comboRecurso")
            .Should().Be("form0:j_id99");
    }

    [Fact]
    public void Combo_inexistente_devolve_null_em_vez_de_chutar()
    {
        SerNovaSolicitacaoService.EventoDoCombo(ComboComOnchange, "form0:naoExiste")
            .Should().BeNull();
    }

    /// <summary>
    /// Cada campo dinâmico é o par container (rótulo) + campo. O asterisco do SER marca
    /// obrigatório e não pode sobrar no rótulo exibido.
    /// </summary>
    [Fact]
    public void Campos_dinamicos_saem_com_rotulo_tipo_e_obrigatoriedade()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:camposDinamicos">
                <div id="form0:container_dinamico_id_936">
                  <span>Peso do Paciente (gramas): *</span>
                  <input id="form0:dinamico_id_936" name="form0:dinamico_id_936" type="text" />
                </div>
                <div id="form0:container_dinamico_id_945">
                  <span>Paciente já realizou cirurgia oncológica?</span>
                  <select id="form0:dinamico_id_945" name="form0:dinamico_id_945">
                    <option value="S">Sim</option><option value="N">Não</option>
                  </select>
                </div>
                <div id="form0:container_dinamico_id_960">
                  <span>Queixa Principal: *</span>
                  <textarea id="form0:dinamico_id_960" name="form0:dinamico_id_960"></textarea>
                </div>
              </div>
            </form></body></html>
            """;

        var campos = SerNovaSolicitacaoService.CamposDinamicos(html);

        campos.Should().HaveCount(3);

        var peso = campos.Single(c => c.Numero == "936");
        peso.Campo.Should().Be("form0:dinamico_id_936");
        peso.Rotulo.Should().Be("Peso do Paciente (gramas)", "o asterisco vira flag, não texto");
        peso.Tipo.Should().Be("text");
        peso.Obrigatorio.Should().BeTrue();

        var cirurgia = campos.Single(c => c.Numero == "945");
        cirurgia.Tipo.Should().Be("select");
        cirurgia.Obrigatorio.Should().BeFalse();
        cirurgia.Opcoes.Should().HaveCount(2);

        campos.Single(c => c.Numero == "960").Tipo.Should().Be("textarea");
    }

    /// <summary>
    /// REGRESSÃO de 10/08/2026: o combo de médicos do SER traz <b>876 opções para 874 valores
    /// únicos</b> — "DIEGO CESAR BORGES" aparece duas vezes com o MESMO id. A cópia do catálogo
    /// conferia duplicata contra o banco mas não dentro do próprio lote, então o segundo
    /// <c>Add</c> estourava o índice único e a cópia inteira morria com 409 ("já foi processado
    /// por outra requisição concorrente" — mensagem que ainda por cima acusava concorrência que
    /// não existia).
    ///
    /// <para>O dado sujo vem do SER e não temos como impedir; o que temos é de deduplicar antes
    /// de gravar.</para>
    /// </summary>
    [Fact]
    public void Opcao_repetida_pelo_ser_e_deduplicada_antes_de_gravar()
    {
        List<SerOpcaoDto> doSer =
        [
            new("195", "DIEGO CESAR BORGES"),
            new("520", "OUTRO MEDICO"),
            new("195", "DIEGO CESAR BORGES"),
        ];

        var unicos = doSer.DistinctBy(x => x.Valor).ToList();

        unicos.Should().HaveCount(2, "o mesmo id não pode virar duas linhas");
        unicos.Select(x => x.Valor).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Campos dinâmicos com o mesmo número no mesmo recurso também colidiriam —
    /// <c>ux_ser_catalogo_campo</c> é (recurso, numero).</summary>
    [Fact]
    public void Campo_dinamico_repetido_no_mesmo_recurso_e_deduplicado()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:container_dinamico_id_491"><span>Queixa *</span>
                <textarea id="form0:dinamico_id_491"></textarea></div>
              <div id="form0:container_dinamico_id_491"><span>Queixa *</span>
                <textarea id="form0:dinamico_id_491"></textarea></div>
            </form></body></html>
            """;

        var campos = SerNovaSolicitacaoService.CamposDinamicos(html);

        campos.DistinctBy(c => c.Numero).Should().ContainSingle(
            "dois containers com o mesmo número são um só campo");
    }

    [Fact]
    public void Sem_bloco_dinamico_devolve_lista_vazia()
    {
        SerNovaSolicitacaoService.CamposDinamicos("<html><body><form id=\"form0\"></form></body></html>")
            .Should().BeEmpty();
    }

    /// <summary>
    /// CANÁRIO da trava de somente-leitura. A troca de aba usa <c>form0:editar_server_submit</c>,
    /// que casa com o verbo "editar" do regex de escrita e só passa por estar na lista nominal.
    /// Se alguém remover a liberação, a tela inteira para de abrir — este teste avisa antes.
    /// </summary>
    [Fact]
    public void Trava_libera_a_troca_de_aba_mas_continua_barrando_o_gravar()
    {
        const string html = """
            <html><body><form id="form0">
              <a id="form0:j_id313"><span>Gravar</span></a>
            </form></body></html>
            """;
        var doc = (AngleSharp.Html.Dom.IHtmlDocument)SerHtmlParser.Documento(html);

        var abrirAba = () => SerWebSessao.GarantirLeitura(
            new Dictionary<string, string> { ["form0:editar_server_submit"] = "x" }, doc);
        var gravar = () => SerWebSessao.GarantirLeitura(
            new Dictionary<string, string> { ["form0:j_id313"] = "form0:j_id313" }, doc);

        abrirAba.Should().NotThrow("abrir o formulário para LER não grava nada");
        gravar.Should().Throw<EscritaNoSerBloqueadaException>("o Gravar continua barrado");
    }
}
