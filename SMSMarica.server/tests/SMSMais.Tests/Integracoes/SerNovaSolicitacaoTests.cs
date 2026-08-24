using FluentAssertions;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;

namespace SMSMais.Tests.Integracoes;

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

    /// <summary>
    /// REGRESSÃO de 10/08/2026, dois defeitos no mesmo campo. Campo de data é um `rich:calendar`,
    /// que embute um &lt;script&gt; DENTRO do container e posta num input irmão.
    ///
    /// <para>(1) O rótulo vinha com o script junto — "Data da coleta da biópsia://&lt;![CDATA[
    /// Richfaces.Calendar.addLocale('pt', {'weekDayLabels':[…" — porque `TextContent` engole
    /// script. (2) Pior: o nome gravado era o id BASE, mas o SER lê o valor do input terminado em
    /// <c>InputDate</c>. A data iria para um campo ignorado e o pedido seria recusado por falta
    /// de um dado que a tela mostrava preenchido.</para>
    ///
    /// <para><c>InputCurrentDate</c> existe no mesmo componente e não é o campo — por isso a
    /// escolha é pelo fim exato do nome.</para>
    /// </summary>
    [Fact]
    public void Campo_de_data_sai_com_rotulo_limpo_e_com_o_input_que_o_ser_le()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:container_dinamico_id_948">
                <span>Data da coleta da biópsia:</span>
                <input id="form0:dinamico_id_948InputDate" name="form0:dinamico_id_948InputDate" type="text" />
                <input id="form0:dinamico_id_948InputCurrentDate" name="form0:dinamico_id_948InputCurrentDate" type="hidden" />
                <script type="text/javascript">//<![CDATA[
                  Richfaces.Calendar.addLocale('pt', {'weekDayLabels':['Domingo','Segunda-feira']});
                  new Calendar('form0:dinamico_id_948', "pt", {'datePattern':'dd/MM/yyyy'}).load();
                //]]></script>
              </div>
            </form></body></html>
            """;

        var campo = SerNovaSolicitacaoService.CamposDinamicos(html).Single();

        campo.Rotulo.Should().Be("Data da coleta da biópsia", "o script não pode virar rótulo");
        campo.Rotulo.Should().NotContain("CDATA").And.NotContain("Richfaces");
        campo.Campo.Should().Be("form0:dinamico_id_948InputDate", "é onde o SER lê a data");
        campo.Campo.Should().NotContain("InputCurrentDate");
        campo.Tipo.Should().Be("date");
    }

    /// <summary>
    /// REGRESSÃO de 10/08/2026. Estrutura COPIADA da captura real (CONSULTA 995, campo 379):
    /// radio no SER é um &lt;table&gt; com um input por opção, todos com o MESMO <c>name</c>, e o
    /// texto de cada opção num <c>&lt;label for&gt;</c>.
    ///
    /// <para>O extrator só lia <c>&lt;option&gt;</c> — que radio não tem — e gravou 56 radios e 4
    /// checkboxes com ZERO opções. A tela caía no input de texto livre: o operador DIGITARIA onde
    /// o SER exige escolha entre valores fixos, e o pedido voltaria recusado. De quebra, as
    /// opções vinham grudadas no rótulo.</para>
    /// </summary>
    [Fact]
    public void Radio_sai_com_opcoes_e_sem_elas_grudadas_no_rotulo()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:container_dinamico_id_379">
                <div id="form0:grupo_dinamico_id_379">
                  <span><label style="text-align:left;">Grupo Sanguineo:</label></span>
                  <table class="radioButton" id="form0:dinamico_id_379"><tbody><tr>
                    <td><input id="form0:dinamico_id_379:0" name="form0:dinamico_id_379" type="radio" value="Tipo A"/>
                        <label for="form0:dinamico_id_379:0">Tipo A</label></td>
                    <td><input id="form0:dinamico_id_379:1" name="form0:dinamico_id_379" type="radio" value="Tipo AB"/>
                        <label for="form0:dinamico_id_379:1">Tipo AB</label></td>
                  </tr></tbody></table>
                </div>
              </div>
            </form></body></html>
            """;

        var campo = SerNovaSolicitacaoService.CamposDinamicos(html).Single();

        campo.Tipo.Should().Be("radio");
        campo.Campo.Should().Be("form0:dinamico_id_379", "todas as opções postam no mesmo nome");
        campo.Rotulo.Should().Be("Grupo Sanguineo", "as opções não são parte do rótulo");
        campo.Rotulo.Should().NotContain("Tipo A");
        campo.Opcoes.Should().BeEquivalentTo(new[]
        {
            new SerOpcaoDto("Tipo A", "Tipo A"),
            new SerOpcaoDto("Tipo AB", "Tipo AB"),
        }, o => o.WithStrictOrdering());
    }

    /// <summary>Checkbox segue a mesma estrutura, e o asterisco continua virando flag.</summary>
    [Fact]
    public void Checkbox_sai_como_multipla_escolha_com_obrigatoriedade()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:container_dinamico_id_594">
                <span><label>Comorbidades:</label><label style="color:red;">*</label></span>
                <table class="checkBox" id="form0:dinamico_id_594"><tbody><tr>
                  <td><input id="form0:dinamico_id_594:0" name="form0:dinamico_id_594" type="checkbox" value="Diabetes"/>
                      <label for="form0:dinamico_id_594:0">Diabetes</label></td>
                  <td><input id="form0:dinamico_id_594:1" name="form0:dinamico_id_594" type="checkbox" value="Doenças articulares"/>
                      <label for="form0:dinamico_id_594:1">Doenças articulares</label></td>
                </tr></tbody></table>
              </div>
            </form></body></html>
            """;

        var campo = SerNovaSolicitacaoService.CamposDinamicos(html).Single();

        campo.Tipo.Should().Be("checkbox");
        campo.Rotulo.Should().Be("Comorbidades");
        campo.Obrigatorio.Should().BeTrue();
        campo.Opcoes.Should().HaveCount(2);
        campo.Opcoes!.Select(o => o.Valor).Should().Contain("Doenças articulares");
    }

    /// <summary>
    /// CANÁRIO: o &lt;label for&gt; só pode ser descartado em grupo de marcação. Num campo comum
    /// ele seria o rótulo de verdade, e removê-lo deixaria o campo anônimo na tela.
    /// </summary>
    [Fact]
    public void Rotulo_de_campo_comum_com_for_nao_e_descartado()
    {
        const string html = """
            <html><body><form id="form0">
              <div id="form0:container_dinamico_id_695">
                <label for="form0:dinamico_id_695">Telefone de contato:</label>
                <input id="form0:dinamico_id_695" name="form0:dinamico_id_695" type="text" />
              </div>
            </form></body></html>
            """;

        SerNovaSolicitacaoService.CamposDinamicos(html).Single()
            .Rotulo.Should().Be("Telefone de contato");
    }

    /// <summary>
    /// O SER recebe múltipla escolha como o MESMO nome repetido. O rascunho guarda par
    /// nome→valor, então os valores viajam juntos e só se desdobram no envio.
    /// </summary>
    [Fact]
    public void Multipla_escolha_vai_e_volta_sem_perder_opcao()
    {
        string[] marcadas = ["Diabetes", "Doenças articulares", "Depressão"];

        SerValorMultiplo.Separar(SerValorMultiplo.Juntar(marcadas))
            .Should().BeEquivalentTo(marcadas, o => o.WithStrictOrdering());

        SerValorMultiplo.Separar("Tipo A").Should().ContainSingle()
            .Which.Should().Be("Tipo A", "valor único não precisa saber que existe separador");
        SerValorMultiplo.Separar(null).Should().BeEmpty();
    }

    /// <summary>
    /// O combo "É AMBULATÓRIO ESTADUAL?" — o primeiro campo da aba — usa a string literal
    /// <c>"null"</c> como placeholder, e não o <c>NoSelectionConverter</c> do Seam que os outros
    /// combos usam. Sem filtrar, "Selecione..." viraria uma terceira alternativa na tela ao lado
    /// de Sim e Não, e o operador poderia escolhê-la.
    ///
    /// <para>Marcação copiada da captura real de 10/08/2026.</para>
    /// </summary>
    [Fact]
    public void Placeholder_literal_null_nao_vira_opcao()
    {
        const string html = """
            <html><body><form id="form0">
              <label>&Eacute; AMBULAT&Oacute;RIO ESTADUAL?</label>
              <select id="form0:comboSisReg" name="form0:comboSisReg">
                <option value="null">Selecione...</option>
                <option value="true">Sim</option>
                <option value="false">N&atilde;o</option>
              </select>
            </form></body></html>
            """;
        var doc = (AngleSharp.Html.Dom.IHtmlDocument)SerHtmlParser.Documento(html);

        var opcoes = SerNovaSolicitacaoService.Combo(doc, "form0:comboSisReg");

        opcoes.Should().BeEquivalentTo(new[]
        {
            new SerOpcaoDto("true", "Sim"),
            new SerOpcaoDto("false", "Não"),
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Sem_bloco_dinamico_devolve_lista_vazia()
    {
        SerNovaSolicitacaoService.CamposDinamicos("<html><body><form id=\"form0\"></form></body></html>")
            .Should().BeEmpty();
    }

    // ------------------------------------------------------------------ tipo do recurso

    /// <summary>
    /// REGRESSÃO de 20/08/2026. A tela manda o tipo no vocabulário do NOSSO domínio
    /// (<c>Consulta</c>/<c>Exame</c>, o enum <c>TipoRecursoSer</c>) e o combo do SER só conhece
    /// <c>CONSULTA</c>/<c>EXAME</c>. O valor cru era repassado ao SER, que não recusa: ele apenas
    /// não aplica a troca e devolve a view intacta. Daí o combo de recurso vinha vazio, o recurso
    /// não amarrava e o autocomplete respondia "Nenhum CID encontrado" para QUALQUER termo —
    /// inclusive para um código que aquele recurso aceita.
    /// </summary>
    [Theory]
    [InlineData("Consulta", "CONSULTA")]
    [InlineData("CONSULTA", "CONSULTA")]
    [InlineData("consulta", "CONSULTA")]
    [InlineData("Exame", "EXAME")]
    [InlineData("EXAME", "EXAME")]
    [InlineData(" exame ", "EXAME")]
    public void Tipo_da_tela_vira_o_vocabulario_do_SER(string daTela, string esperado)
    {
        SerNovaSolicitacaoService.TipoParaOSer(daTela).Should().Be(esperado);
    }

    /// <summary>
    /// Tipo desconhecido FALHA em vez de seguir: mandá-lo ao SER devolveria uma tela sem recurso,
    /// que é indistinguível de "este recurso não tem CID".
    /// </summary>
    [Fact]
    public void Tipo_desconhecido_e_recusado_em_vez_de_ir_ao_SER()
    {
        var acao = () => SerNovaSolicitacaoService.TipoParaOSer("Procedimento");

        acao.Should().Throw<ValidacaoException>()
            .Which.Erros.Should().ContainKey("ser.tipo_invalido");
    }

    // ------------------------------------------------------------------ Hipótese (CID)

    /// <summary>
    /// Resposta real do autocomplete de CID em 20/08/2026 (recurso 1003, termo "A09"). A primeira
    /// célula é a coluna OCULTA — o texto que o navegador escreve no campo — e a tabela traz
    /// sempre a linha escondida <c>NothingLabel</c>, mesmo quando há resultado.
    /// </summary>
    private const string TabelaDeCid = """
        <html><body>
        <table id="form0:j_id226:suggest" class="rich-sb-int-decor-table"><tbody>
          <tr class="rich-sb-int richfaces_suggestionEntry">
            <td style="display: none;">(A09 ) Diarréia e gastroenterite de origem infecciosa presumível</td>
            <td nowrap="nowrap" class="rich-sb-cell-padding">A09</td>
            <td nowrap="nowrap" class="rich-sb-cell-padding">Diarréia e gastroenterite de origem infecciosa presumível</td>
          </tr>
          <tr id="form0:j_id226:0NothingLabel" class="rich-sb-int" style="display: none;">
            <td nowrap="nowrap" class="rich-sb-cell-padding">Nenhum CID encontrado</td>
          </tr>
        </tbody></table>
        </body></html>
        """;

    /// <summary>
    /// O que vai de volta no campo é a COLUNA OCULTA, não o código nem a descrição. Guardar outra
    /// coisa faz o SER gravar o pedido sem hipótese e responder "salva com sucesso" — foi o que
    /// aconteceu na edição de 10/08/2026.
    /// </summary>
    [Fact]
    public void Cid_guarda_o_texto_da_coluna_oculta_que_o_SER_espera_de_volta()
    {
        var doc = (AngleSharp.Html.Dom.IHtmlDocument)SerHtmlParser.Documento(TabelaDeCid);
        var linhas = SerHtmlParser.LinhasDeSugestao(doc, "form0:j_id226")!;

        var cids = SerNovaSolicitacaoService.CidsDaTabela(linhas);

        cids.Should().ContainSingle();
        cids[0].Codigo.Should().Be("A09");
        cids[0].Descricao.Should().Be("Diarréia e gastroenterite de origem infecciosa presumível");
        cids[0].Texto.Should().Be("(A09 ) Diarréia e gastroenterite de origem infecciosa presumível");
    }

    /// <summary>
    /// A linha "Nenhum CID encontrado" vem escondida em TODA resposta, inclusive nas que têm
    /// resultado. Se ela vazasse para a lista, o operador poderia escolhê-la como diagnóstico.
    /// </summary>
    [Fact]
    public void Linha_de_nada_encontrado_nunca_vira_um_CID()
    {
        const string vazia = """
            <html><body>
            <table id="form0:j_id226:suggest"><tbody>
              <tr id="form0:j_id226:0NothingLabel" style="display: none;">
                <td>Nenhum CID encontrado</td>
              </tr>
            </tbody></table>
            </body></html>
            """;
        var doc = (AngleSharp.Html.Dom.IHtmlDocument)SerHtmlParser.Documento(vazia);

        SerNovaSolicitacaoService
            .CidsDaTabela(SerHtmlParser.LinhasDeSugestao(doc, "form0:j_id226")!)
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
