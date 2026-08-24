using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O ciclo de leitura de histórico: pesquisa por ID → abre o histórico → pesquisa o próximo.
///
/// <para>É o ciclo mais frágil do motor, porque ele <b>alterna entre duas views JSF</b> — a tela
/// de pesquisa e a de histórico — e o ViewState de uma não serve para a outra.</para>
/// </summary>
public class SerLeitorServiceTests
{
    private const string ViewStatePesquisa = "j_id5";
    private const string ViewStateHistorico = "j_id6";

    private static string TelaPesquisa(string viewState, bool comLinha) => $"""
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
          <input type="hidden" name="form0" value="form0" />
          <input type="hidden" name="javax.faces.ViewState" value="{viewState}" />
          <select name="form0:j_id75"><option value="CANCELADA">Cancelada</option></select>
          <input id="form0:idSolicitacao" name="form0:idSolicitacao" value="" />
          <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
          <table id="form0:listagem">
            <thead><tr><th>ID</th><th>Tipo</th><th>Recurso</th><th>Data da Solicitação</th><th>Paciente</th></tr></thead>
            <tbody>{(comLinha ? """
              <tr><td>3853975</td><td>CONSULTA</td><td>X</td><td>01/02/2023</td><td>FULANO</td></tr>
              """ : "")}</tbody>
          </table>
          {(comLinha ? """<a id="form0:listagem:0:j_id165">Historico da Solicitação</a>""" : "")}
        </form></body></html>
        """;

    /// <summary>
    /// A tela de histórico é OUTRA view: tem `form0` (as abas vivem nele) mas <b>não</b> tem o
    /// botão Pesquisar — e o ViewState dela é outro.
    /// </summary>
    private const string TelaHistorico = $"""
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-historico.seam">
          <input type="hidden" name="javax.faces.ViewState" value="{ViewStateHistorico}" />
          <td id="form0:pesquisar_cell"><span id="form0:pesquisar_lbl">Pesquisar</span></td>
          <table id="form0:historicoList">
            <thead><tr><th>Data</th><th>Evento</th></tr></thead>
            <tbody><tr><td>03/08/2022 12:56:36</td><td>Solicitar</td></tr></tbody>
          </table>
        </form></body></html>
        """;

    /// <summary>
    /// REGRESSÃO de 08/08/2026, medida contra o SER: a mesma busca por ID devolve <b>1 linha</b>
    /// com o ViewState da tela de pesquisa e <b>0</b> com o da tela de histórico.
    ///
    /// <para>Absorver o ViewState de qualquer página deixava <c>_htmlForm</c> (pesquisa) e o
    /// ViewState (histórico) em views diferentes; o JSF restaurava a errada, a ação não rodava e a
    /// grade voltava vazia — HTTP 200, sem erro. Na carga inicial isso deu <b>14.363 falhas</b>:
    /// o primeiro histórico era lido e todos os seguintes morriam com "devolveu 0 linhas".</para>
    /// </summary>
    [Fact]
    public async Task Viewstate_da_tela_de_historico_nao_contamina_a_busca_seguinte()
    {
        var sessao = new SessaoFalsa(
            tela: TelaPesquisa(ViewStatePesquisa, comLinha: false),
            respostas:
            [
                TelaPesquisa(ViewStatePesquisa, comLinha: true),   // 1ª busca
                TelaHistorico,                                     // abre o histórico (OUTRA view)
                TelaPesquisa(ViewStatePesquisa, comLinha: true),   // 2ª busca
                TelaHistorico,
            ]);
        var leitor = new SerLeitorService(sessao, NullLogger<SerLeitorService>.Instance);

        await leitor.LerHistoricoPorIdAsync("3853975", SituacaoSer.Cancelada, CancellationToken.None);
        await leitor.LerHistoricoPorIdAsync("3853975", SituacaoSer.Cancelada, CancellationToken.None);

        sessao.ViewStatesEnviados.Should().HaveCount(4);
        sessao.ViewStatesEnviados[2].Should().Be(
            ViewStatePesquisa,
            "a 2ª busca tem de usar o ViewState da tela de PESQUISA — o da tela de histórico é de "
            + "outra view e faz o JSF devolver grade vazia sem erro");
        sessao.ViewStatesEnviados.Should().NotContain(ViewStateHistorico);
    }

    /// <summary>
    /// A resposta do datascroller é PARCIAL (mesma view, sem `form0`) e traz o ViewState novo —
    /// esse tem de ser aceito, senão a paginação quebra. A recusa vale só para OUTRA página
    /// completa.
    /// </summary>
    [Fact]
    public async Task Viewstate_de_resposta_parcial_continua_sendo_aceito()
    {
        const string parcial = """
            <html><body>
              <input type="hidden" name="javax.faces.ViewState" value="j_id9" />
              <table id="form0:listagem">
                <thead><tr><th>ID</th><th>Paciente</th></tr></thead>
                <tbody><tr><td>111111</td><td>BELTRANO</td></tr></tbody>
              </table>
            </body></html>
            """;
        var sessao = new SessaoFalsa(
            tela: TelaPesquisa(ViewStatePesquisa, comLinha: false),
            respostas: [TelaPesquisa(ViewStatePesquisa, comLinha: true), parcial, parcial]);
        var leitor = new SerLeitorService(sessao, NullLogger<SerLeitorService>.Instance);

        await leitor.PesquisarAsync(
            new SerFiltroPesquisa { Situacao = SituacaoSer.Cancelada }, CancellationToken.None);
        await leitor.IrParaPaginaAsync(2, CancellationToken.None);
        await leitor.IrParaPaginaAsync(3, CancellationToken.None);

        sessao.ViewStatesEnviados[1].Should().Be(ViewStatePesquisa);
        sessao.ViewStatesEnviados[2].Should().Be("j_id9", "o parcial re-renderiza a MESMA view");
    }

    // ------------------------------------------------------------------ dublê

    private sealed class SessaoFalsa(string tela, params string[] respostas) : ISerWebSessao
    {
        private readonly Queue<string> _respostas = new(respostas);

        public List<string?> ViewStatesEnviados { get; } = [];

        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) =>
            Task.FromResult(tela);

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) =>
            Task.FromResult(tela);

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken)
        {
            ViewStatesEnviados.Add(viewState);
            return Task.FromResult(_respostas.Count > 0 ? _respostas.Dequeue() : "<html></html>");
        }

        public Task<RespostaSer> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, CancellationToken cancellationToken)
        {
            ViewStatesEnviados.Add(viewState);
            var corpo = _respostas.Count > 0 ? _respostas.Dequeue() : "<html></html>";
            return Task.FromResult(new RespostaSer(Encoding.UTF8.GetBytes(corpo), "text/html", null, null));
        }

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
