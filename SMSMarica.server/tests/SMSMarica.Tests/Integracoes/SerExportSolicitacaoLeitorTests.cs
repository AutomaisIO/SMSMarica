using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Integracoes.SerWeb.Varredura;
using SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Leitura de ALTA pelo export da tela de Solicitação (08/08/2026).
///
/// <para>ALTA não existe no combo da tela de Histórico, então só pode ser lida aqui — e esta tela
/// corta em 100 <b>sem avisar</b>. Em 07/08/2026 a varredura perdeu 853 registros de ALTA
/// declarando cobertura completa, lendo de 20 em 20; o que está sob teste é o caminho que
/// substituiu aquela paginação.</para>
/// </summary>
public class SerExportSolicitacaoLeitorTests
{
    private const string Tela = """
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam;jsessionid=ABC">
          <input type="hidden" name="form0" value="form0" />
          <input type="hidden" name="javax.faces.ViewState" value="j_id5" />
          <select name="form0:j_id75">
            <option value="EM_FILA">Em fila</option>
            <option value="ALTA">Alta</option>
          </select>
          <select name="form0:comboTipoRecurso">
            <option value="" selected="selected"></option>
            <option value="CONSULTA">CONSULTA</option>
            <option value="EXAME">EXAME</option>
          </select>
          <input id="form0:dtInicialSolicitacaoInputDate" name="form0:dtInicialSolicitacaoInputDate" value="" />
          <input id="form0:dtFinalSolicitacaoInputDate" name="form0:dtFinalSolicitacaoInputDate" value="" />
          <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
        </form></body></html>
        """;

    private const string ResultadoVazio = """
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
          <input type="hidden" name="javax.faces.ViewState" value="j_id5" />
          <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
          <ul id="form0:messages"></ul>
        </form></body></html>
        """;

    /// <summary>Recusa do SER: mensagem escrita E nenhuma linha. Sem tratar, isso chegaria como
    /// "recorte vazio" e o cursor passaria por cima de registros nunca lidos.</summary>
    private const string ResultadoRecusado = """
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
          <input type="hidden" name="javax.faces.ViewState" value="j_id5" />
          <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
          <ul id="form0:messages"><li>Informe ao menos um filtro para realizar a consulta.</li></ul>
        </form></body></html>
        """;

    private static SerFiltroExport Filtro(TipoRecursoSer? tipo = null) => new()
    {
        Situacao = SituacaoSer.Alta,
        Tipo = tipo,
        DataSolicitacaoInicio = new DateOnly(2020, 1, 1),
        DataSolicitacaoFim = new DateOnly(2020, 12, 31),
    };

    private static SerExportSolicitacaoLeitor Leitor(SessaoFalsa sessao) =>
        new(sessao, NullLogger<SerExportSolicitacaoLeitor>.Instance);

    [Fact]
    public void Teto_e_de_100_e_a_tela_se_identifica()
    {
        var leitor = Leitor(new SessaoFalsa(Tela));

        leitor.TetoPorLote.Should().Be(100, "5 páginas × 20 — medido: o arquivo respeita o mesmo teto");
        leitor.Tela.Should().Be("de Solicitação");
    }

    /// <summary>
    /// O combo de situação é localizado pelo <c>&lt;select&gt;</c> que oferece ALTA — que é o que
    /// distingue esta tela da de Histórico — e não por um <c>j_id</c> lido numa captura, que muda
    /// quando a SES-RJ recompila a página.
    /// </summary>
    [Fact]
    public async Task Busca_manda_alta_e_as_datas_e_nao_manda_solicitante()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio);

        await Leitor(sessao).ExportarAsync(Filtro(), CancellationToken.None);

        sessao.Submits.Should().ContainSingle("sem linhas na grade não há o que exportar");
        var busca = sessao.Submits[0];
        busca["form0:j_id75"].Should().Be("ALTA");
        busca["form0:dtInicialSolicitacaoInputDate"].Should().Be("01/01/2020");
        busca["form0:dtFinalSolicitacaoInputDate"].Should().Be("31/12/2020");
        busca["AJAXREQUEST"].Should().Be("form0");
        busca.Should().ContainKey("form0:j_id96");

        // Esta tela não tem campo de solicitante: o recorte de Maricá vem da credencial do
        // operador — provado pelas colunas Solicitante/Município Solicitante do próprio arquivo.
        busca.Should().NotContainKey("form0:suggUnidadeSol");
        busca.Should().NotContainKey("inputvalue");
    }

    /// <summary>O corte por Tipo é o último recurso da bisecção, e AQUI ele funciona de verdade
    /// (na tela de Histórico o mesmo combo é decorativo).</summary>
    [Fact]
    public async Task Busca_manda_o_tipo_quando_a_bisseccao_fatia_por_ele()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio);

        await Leitor(sessao).ExportarAsync(Filtro(TipoRecursoSer.Consulta), CancellationToken.None);

        sessao.Submits[0]["form0:comboTipoRecurso"].Should().Be("CONSULTA");
    }

    [Fact]
    public async Task Recusa_escrita_do_ser_sem_linhas_vira_falha_explicita()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoRecusado);

        var acao = () => Leitor(sessao).ExportarAsync(Filtro(), CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidacaoException>())
            .Which.Message.Should().Contain("Informe ao menos um filtro");
    }

    /// <summary>
    /// REGRESSÃO do incidente de 08/08/2026. A coluna "Agendado para" deste export é um FLUXO que
    /// não se alinha com as linhas de registro (os 100 registros vêm primeiro, os fragmentos
    /// depois, num bloco) e o número de linhas por registro é variável — não há como remontar. A
    /// tentativa de remontar grudou agendamentos alheios num só registro, estourou o
    /// <c>varchar(300)</c> e derrubou a varredura em produção.
    ///
    /// <para>Este teste fixa o contrato: <b>esta tela não devolve agendamento</b>. Quem preenche
    /// essa coluna é a de Histórico, que traz a data em campo próprio e alinhado.</para>
    /// </summary>
    [Fact]
    public void Contrato_desta_tela_nao_inclui_agendamento()
    {
        typeof(SerExportSolicitacaoLeitor)
            .GetMethod(nameof(SerExportSolicitacaoLeitor.ExportarAsync))
            .Should().NotBeNull();

        // O merge no espelho não pode APAGAR o agendamento que a outra tela trouxe quando esta
        // manda nulo — é o `??` de PreencherDaGrade que garante isso.
        var lida = new SerLinhaGrade { IdSer = "1", AgendadoPara = null };
        (lida.AgendadoPara ?? "28/01/2020").Should().Be("28/01/2020");
    }

    [Fact]
    public async Task Sem_linhas_devolve_lote_vazio_e_nao_truncado()
    {
        var lote = await Leitor(new SessaoFalsa(Tela, ResultadoVazio))
            .ExportarAsync(Filtro(), CancellationToken.None);

        lote.Linhas.Should().BeEmpty();
        lote.Truncado.Should().BeFalse();
    }

    /// <summary>
    /// Reter a tela do lote anterior faria a busca seguinte sair da conversa Seam velha. Quando o
    /// GET não devolve a tela de pesquisa — quase sempre a de login, com HTTP 200, porque outro
    /// login do mesmo operador derrubou a sessão — a leitura para ali.
    /// </summary>
    [Fact]
    public async Task Preparar_falha_quando_o_get_nao_devolve_a_tela_de_pesquisa()
    {
        var sessao = new SessaoFalsa("<html><body>tela de login</body></html>");

        var acao = () => Leitor(sessao).PrepararAsync(CancellationToken.None);

        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("Pesquisar");
    }

    /// <summary>Cada lote começa com um GET novo: encadear submits a partir da página de resultado
    /// reaproveita a conversa anterior, e foi assim que a tela irmã passou a devolver conjuntos
    /// instáveis entre chamadas.</summary>
    [Fact]
    public async Task Cada_lote_reabre_a_tela_antes_de_pesquisar()
    {
        var sessao = new SessaoFalsa(Tela, ResultadoVazio, ResultadoVazio);
        var leitor = Leitor(sessao);

        await leitor.ExportarAsync(Filtro(), CancellationToken.None);
        await leitor.ExportarAsync(Filtro(), CancellationToken.None);

        sessao.Gets.Should().HaveCount(2);
        sessao.Gets.Should().AllSatisfy(g =>
            g.Should().Be(SerExportSolicitacaoLeitor.CaminhoTela));
    }

    // ------------------------------------------------------------------ dublê

    private sealed class SessaoFalsa(string tela, params string[] respostas) : ISerWebSessao
    {
        private readonly Queue<string> _respostas = new(respostas);

        public List<string> Gets { get; } = [];
        public List<Dictionary<string, string>> Submits { get; } = [];

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken)
        {
            Gets.Add(caminho);
            return Task.FromResult(tela);
        }

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

        /// <summary>Estes testes exercitam LEITURA. Se algum caminho chamar a escrita, é bug do
        /// código sob teste — falhar alto aqui é o que denuncia.</summary>
        public Task<RespostaSer> SubmeterEscritaAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, string operacao, CancellationToken cancellationToken) =>
            throw new NotSupportedException($"dublê de leitura recebeu escrita: {operacao}");

        public void UsarCredencialDoOperador(string usuario, string senha) =>
            throw new NotSupportedException("dublê de leitura não autentica operador");


        public void Reiniciar()
        {
        }
    }
}
