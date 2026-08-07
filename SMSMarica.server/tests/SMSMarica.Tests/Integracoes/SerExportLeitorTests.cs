using System.Text;
using AngleSharp.Html.Dom;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Testes da amarração do filtro de Solicitante no export do SER.
///
/// <para>O que está sob teste é o achado de 07/08/2026: o SER só aplica o filtro de Solicitante
/// durante a ida-e-volta A4J do autocomplete (fetch + onselect com o índice no hidden
/// <c>_selection</c>) — texto puro é decorativo e a consulta sai do <b>Estado inteiro</b>, com PII
/// de outros municípios. O contrato aqui é duplo: (1) a sequência e os parâmetros das requisições
/// são exatamente os do navegador; (2) quando a sugestão não vem, a leitura FALHA em vez de
/// degradar para busca sem filtro.</para>
/// </summary>
public class SerExportLeitorTests
{
    private const string Solicitante = "GESTOR SMS MARICA";

    /// <summary>Tela de Histórico com a ESTRUTURA da captura real de 06/08/2026 — inclusive o
    /// script de init do suggestionbox, de onde os ids voláteis são extraídos.</summary>
    private const string Tela = """
        <html><body>
        <form id="form0" action="/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam;jsessionid=XYZ">
          <input type="hidden" name="form0" value="form0" />
          <input type="hidden" name="javax.faces.ViewState" value="j_id5" />
          <select name="form0:j_id57">
            <option value="EM_FILA">Em fila</option>
            <option value="AGENDADA">Agendada</option>
          </select>
          <select name="form0:tipo">
            <option value="" selected="selected"></option>
            <option value="CONSULTA">CONSULTA</option>
            <option value="EXAME">EXAME</option>
          </select>
          <input id="form0:dataInicialInputDate" name="form0:dataInicialInputDate" value="" />
          <input id="form0:dataFinalInputDate" name="form0:dataFinalInputDate" value="" />
          <div id="form0:j_id37_script"><script type="text/javascript">
            Richfaces.onAvailable('form0:suggUnidadeSol', function() { new RichFaces.Suggestion('form0','form0:suggUnidadeSol','form0:j_id37',{'similarityGroupingId':'form0:j_id37','onselect':function(suggestion,event){A4J.AJAX.Submit('form0',event,{'similarityGroupingId':'form0:j_id37:j_id42','parameters':{'form0:j_id37:j_id42':'form0:j_id37:j_id42','ajaxSingle':'form0:j_id37'} ,'status':'_viewRoot:status'} )},'implicitEventsQueue':'form0:j_id37','width':'750','parameters':{'form0:j_id37':'form0:j_id37','ajaxSingle':'form0:j_id37'} ,'status':'dd:status','requestDelay':200,'minChars':'1'} );});
          </script></div>
          <input type="hidden" autocomplete="off" id="form0:j_id37_selection" name="form0:j_id37_selection" />
          <input id="form0:suggUnidadeSol" type="text" name="form0:suggUnidadeSol" />
          <a id="form0:btnSearch" title="Pesquisar"><span>Pesquisar</span></a>
        </form></body></html>
        """;

    /// <summary>Resposta parcial do fetch: a linha real vem com o nome repetido em 3 células —
    /// por isso o casamento é pela PRIMEIRA célula, nunca por contains na linha inteira.</summary>
    private const string RespostaFetch = """
        <html><body>
        <table id="form0:j_id37:suggest"><tbody>
          <tr class="richfaces_suggestionEntry"><td>GESTOR SMS MARICA</td><td>MARICA</td><td>GESTOR SMS MARICA</td></tr>
          <tr class="richfaces_suggestionEntry"><td>Nenhum registro encontrado</td></tr>
        </tbody></table>
        <input type="hidden" name="javax.faces.ViewState" value="j_id6" />
        </body></html>
        """;

    private const string RespostaFetchSemAlvo = """
        <html><body>
        <table id="form0:j_id37:suggest"><tbody>
          <tr class="richfaces_suggestionEntry"><td>Nenhum registro encontrado</td></tr>
        </tbody></table>
        </body></html>
        """;

    /// <summary>Resposta boa do onselect: envelope A4J (medido na sonda de 07/08/2026). A tela de
    /// login — sessão derrubada — não traz o meta, e é assim que o leitor a reconhece.</summary>
    private const string RespostaOnselect = """
        <html><head><meta name="Ajax-Response" content="true" /></head>
        <body><input type="hidden" name="javax.faces.ViewState" value="j_id7" /></body></html>
        """;

    private const string RespostaOnselectSemEnvelope = """
        <html><body><form id="login"><input id="login:username" /></form></body></html>
        """;

    private const string RespostaBuscaRedirect = """
        <html><head>
          <meta name="Ajax-Response" content="redirect" />
          <meta name="Location" content="/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam?cid=999" />
        </head></html>
        """;

    /// <summary>Página de resultado SEM linhas: o leitor devolve lote vazio e não exporta —
    /// suficiente para provar a sequência de requisições sem fabricar um BIFF8.</summary>
    private const string ResultadoVazio = """
        <html><body>
        <form id="form0" action="/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam?cid=999">
          <input type="hidden" name="form0" value="form0" />
          <input type="hidden" name="javax.faces.ViewState" value="j_id8" />
          <a id="form0:btnSearch2" title="Pesquisar"><span>Pesquisar</span></a>
          <ul id="form0:messages"></ul>
        </form></body></html>
        """;

    private static SerFiltroExport Filtro(string? solicitante = Solicitante) => new()
    {
        Situacao = SituacaoSer.Agendada,
        DataSolicitacaoInicio = new DateOnly(2020, 1, 1),
        DataSolicitacaoFim = new DateOnly(2020, 12, 31),
        UnidadeSolicitante = solicitante,
    };

    [Fact]
    public async Task Amarra_o_solicitante_com_fetch_e_onselect_antes_da_busca()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio,
            RespostaFetch, RespostaOnselect, RespostaBuscaRedirect);
        var leitor = new SerExportLeitor(sessao, NullLogger<SerExportLeitor>.Instance);

        var lote = await leitor.ExportarAsync(Filtro(), CancellationToken.None);

        lote.Linhas.Should().BeEmpty();
        sessao.Submits.Should().HaveCount(3, "fetch de sugestões, onselect e busca — nessa ordem");

        var fetch = sessao.Submits[0];
        fetch["AJAXREQUEST"].Should().Be("_viewRoot", "o init do suggestionbox não passa containerId");
        fetch["inputvalue"].Should().Be(Solicitante, "o texto digitado viaja no param default do RichFaces");
        fetch["form0:j_id37"].Should().Be("form0:j_id37");
        fetch["ajaxSingle"].Should().Be("form0:j_id37");

        var onselect = sessao.Submits[1];
        onselect["AJAXREQUEST"].Should().Be("_viewRoot");
        onselect["form0:j_id37:j_id42"].Should().Be("form0:j_id37:j_id42", "é o a4j:support do onselect");
        onselect["ajaxSingle"].Should().Be("form0:j_id37");
        onselect["form0:j_id37_selection"].Should().Be("0",
            "o índice da linha escolhida viaja no hidden _selection durante o submit do onselect");

        var busca = sessao.Submits[2];
        busca.Should().ContainKey("form0:btnSearch");
        busca["AJAXREQUEST"].Should().Be("_viewRoot", "a sequência provada na sonda usa _viewRoot nas três");
        busca.Should().NotContainKey("inputvalue");
        busca.TryGetValue("form0:j_id37_selection", out var selecao);
        (selecao ?? string.Empty).Should().BeEmpty("o navegador limpa o _selection após o onselect");
    }

    /// <summary>
    /// O caso que protege a LGPD: sem sugestão que case, a leitura para ali. Degradar para busca
    /// sem filtro significaria espelhar a fila do Estado inteiro — PII de outros municípios — e
    /// foi exatamente o que a base fez até 07/08/2026 sem ninguém perceber.
    /// </summary>
    [Fact]
    public async Task Falha_alto_quando_o_ser_nao_sugere_o_solicitante()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio, RespostaFetchSemAlvo);
        var leitor = new SerExportLeitor(sessao, NullLogger<SerExportLeitor>.Instance);

        var acao = () => leitor.ExportarAsync(Filtro(), CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Message.Should().Contain(Solicitante);
        sessao.Submits.Should().HaveCount(1, "depois do fetch sem sugestão, NENHUMA busca pode sair");
    }

    [Fact]
    public async Task Sem_solicitante_nao_ha_autocomplete_e_a_busca_sai_como_sempre()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio, RespostaBuscaRedirect);
        var leitor = new SerExportLeitor(sessao, NullLogger<SerExportLeitor>.Instance);

        await leitor.ExportarAsync(Filtro(solicitante: null), CancellationToken.None);

        sessao.Submits.Should().HaveCount(1);
        var busca = sessao.Submits[0];
        busca.Should().ContainKey("form0:btnSearch");
        busca["AJAXREQUEST"].Should().Be("form0", "sem amarração, o caminho antigo continua idêntico");
        busca.Should().NotContainKey("inputvalue");
        busca.Should().NotContainKey("form0:suggUnidadeSol");
    }

    /// <summary>
    /// A resposta boa do onselect é um envelope A4J. Sem ele (tela de login = sessão derrubada),
    /// a amarração NÃO aconteceu — e como o efeito é invisível (estado na conversa Seam), seguir
    /// adiante importaria o Estado inteiro com cara de recorte filtrado.
    /// </summary>
    [Fact]
    public async Task Falha_quando_o_onselect_nao_devolve_o_envelope_a4j()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio,
            RespostaFetch, RespostaOnselectSemEnvelope);
        var leitor = new SerExportLeitor(sessao, NullLogger<SerExportLeitor>.Instance);

        var acao = () => leitor.ExportarAsync(Filtro(), CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
        sessao.Submits.Should().HaveCount(2, "depois do onselect sem envelope, a busca NÃO pode sair");
    }

    /// <summary>
    /// REGRESSÃO do repasse de 07/08/2026: a busca sem redirect A4J era só LogWarning e o fluxo
    /// seguia parseando a resposta (a tela de login, sem grade nem mensagens) como resultado —
    /// "lote vazio, cobertura completa", cursor avançando por cima de recorte nunca lido. Agora é
    /// falha dura: nada foi lido, o recorte fica para a retomada.
    /// </summary>
    [Fact]
    public async Task Falha_quando_a_busca_nao_devolve_o_redirect_a4j()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio,
            RespostaFetch, RespostaOnselect, "<html><body>tela de login</body></html>");
        var leitor = new SerExportLeitor(sessao, NullLogger<SerExportLeitor>.Instance);

        var acao = () => leitor.ExportarAsync(Filtro(), CancellationToken.None);

        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("redirect");
    }

    /// <summary>Reter a tela do lote anterior em silêncio violaria o "GET novo por busca": a
    /// busca sairia da conversa velha e o resultado voltaria instável (docs/ser.md §4.3).</summary>
    [Fact]
    public async Task Preparar_falha_quando_o_get_nao_devolve_a_tela_de_pesquisa()
    {
        var sessao = new SessaoFalsa("<html><body>tela de login</body></html>", ResultadoVazio);
        var leitor = new SerExportLeitor(sessao, NullLogger<SerExportLeitor>.Instance);

        var acao = () => leitor.PrepararAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// CANÁRIO da trava de somente-leitura: os parâmetros da amarração precisam passar. Se um dia
    /// o regex de escrita crescer e pegar algum deles (p.ex. "_selection"), a varredura filtrada
    /// inteira para — este teste avisa antes do deploy.
    /// </summary>
    [Fact]
    public void Trava_permite_os_parametros_da_amarracao()
    {
        var doc = (IHtmlDocument)SerHtmlParser.Documento(Tela);
        var extras = new Dictionary<string, string>
        {
            ["AJAXREQUEST"] = "_viewRoot",
            ["inputvalue"] = Solicitante,
            ["form0:j_id37"] = "form0:j_id37",
            ["form0:j_id37:j_id42"] = "form0:j_id37:j_id42",
            ["ajaxSingle"] = "form0:j_id37",
            ["form0:j_id37_selection"] = "0",
        };

        var acao = () => SerWebSessao.GarantirLeitura(extras, doc);

        acao.Should().NotThrow();
    }

    /// <summary>Sessão caída troca a planilha pela tela de login com HTTP 200 — o reconhecimento
    /// é pela assinatura OLE2 do CONTEÚDO, nunca pelo Content-Type.</summary>
    [Fact]
    public void Planilha_e_reconhecida_pela_assinatura_ole2_e_nao_pelo_content_type()
    {
        var ole2 = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00 };
        new RespostaSer(ole2, "text/html", null, null).EhPlanilha
            .Should().BeTrue("o WildFly do SER responde a planilha com text/html quando quer");

        var html = Encoding.UTF8.GetBytes("<html>login</html>");
        new RespostaSer(html, "application/vnd.ms-excel", null, null).EhPlanilha
            .Should().BeFalse("content-type de planilha não faz de uma tela de login um xls");
    }

    // ------------------------------------------------------------------ parser

    /// <summary>
    /// Os ids do suggestionbox saem do script de init DA PÁGINA — <c>j_id</c> é posicional e muda
    /// quando a SES-RJ recompila. Aqui o box é j_id88/j_id92 de propósito: se alguém hardcodar
    /// j_id37, este teste quebra.
    /// </summary>
    [Fact]
    public void SuggestionBox_e_extraido_do_script_de_init_e_nao_de_id_fixo()
    {
        const string html = """
            <script>new RichFaces.Suggestion('form0','form0:suggUnidadeSol','form0:j_id88',
            {'onselect':function(s,e){A4J.AJAX.Submit('form0',e,{'parameters':
            {'form0:j_id88:j_id92':'form0:j_id88:j_id92','ajaxSingle':'form0:j_id88'}})},
            'parameters':{'form0:j_id88':'form0:j_id88','ajaxSingle':'form0:j_id88'}});</script>
            """;

        var caixa = SerHtmlParser.SuggestionBoxDoCampo(html, "form0:suggUnidadeSol");

        caixa.Should().NotBeNull();
        caixa!.BoxId.Should().Be("form0:j_id88");
        caixa.OnselectId.Should().Be("form0:j_id88:j_id92");
        caixa.CampoSelecao.Should().Be("form0:j_id88_selection");
    }

    [Fact]
    public void SuggestionBox_devolve_null_quando_o_script_nao_esta_na_pagina()
    {
        SerHtmlParser.SuggestionBoxDoCampo("<html><body>nada</body></html>", "form0:suggUnidadeSol")
            .Should().BeNull();
    }

    /// <summary>O índice enviado no _selection é a posição da linha na ORDEM do DOM — o RichFaces
    /// usa <c>tbody.childNodes[i]</c> no clique. Ordem preservada é contrato, não detalhe.</summary>
    [Fact]
    public void Linhas_de_sugestao_preservam_a_ordem_do_dom()
    {
        var doc = (IHtmlDocument)SerHtmlParser.Documento(RespostaFetch);

        var linhas = SerHtmlParser.LinhasDeSugestao(doc, "form0:j_id37");

        linhas.Should().NotBeNull();
        linhas!.Should().HaveCount(2);
        linhas![0][0].Should().Be("GESTOR SMS MARICA");
        linhas[1][0].Should().Be("Nenhum registro encontrado");
    }

    [Fact]
    public void Linhas_de_sugestao_devolvem_null_sem_a_tabela()
    {
        var doc = (IHtmlDocument)SerHtmlParser.Documento("<html><body></body></html>");

        SerHtmlParser.LinhasDeSugestao(doc, "form0:j_id37").Should().BeNull();
    }

    // ------------------------------------------------------------------ dublê

    /// <summary>Sessão falsa: devolve a tela no GET, e as respostas roteirizadas nos submits, na
    /// ordem. Grava os extras de cada submit para as asserções de protocolo.</summary>
    private sealed class SessaoFalsa(string tela, string paginaResultado, params string[] respostas)
        : ISerWebSessao
    {
        private readonly Queue<string> _respostas = new(respostas);

        public List<Dictionary<string, string>> Submits { get; } = [];

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) =>
            Task.FromResult(caminho.Contains("cid=", StringComparison.Ordinal) ? paginaResultado : tela);

        public Task<RespostaSer> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, CancellationToken cancellationToken)
        {
            Submits.Add(new Dictionary<string, string>(extras, StringComparer.Ordinal));
            var corpo = _respostas.Count > 0 ? _respostas.Dequeue() : "<html></html>";
            return Task.FromResult(new RespostaSer(Encoding.UTF8.GetBytes(corpo), "text/html", null, null));
        }

        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> AutenticarAvulsoAsync(
            string usuario, string senha, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Reiniciar()
        {
        }
    }
}
