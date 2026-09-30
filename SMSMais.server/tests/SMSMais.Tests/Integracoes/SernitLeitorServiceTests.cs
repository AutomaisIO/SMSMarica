using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// A pesquisa do SERNIT e a PÁGINA 1 que volta dela.
///
/// <para>REGRESSÃO medida contra o SERNIT em 30/09/2026: pesquisando a partir do form absorvido da
/// resposta anterior (e não de uma tela reaberta), o SERNIT devolve na página 1 a página 1 da PESQUISA
/// ANTERIOR — as páginas 2..N já vêm da nova. A varredura adaptativa sempre pesquisa uma janela menor
/// logo depois de uma janela capada, então a página 1 dessas janelas nunca era lida: o espelho ficou
/// com 1.349 de 1.511 solicitações.</para>
/// </summary>
public class SernitLeitorServiceTests
{
    private const string CampoInicio = "form0:dtInicialSolicitacaoInputDate";

    private static string Pagina(bool fresca, IEnumerable<string> ids, int paginas, int? totalCapado) => $"""
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
          <input type="hidden" name="form0" value="form0" />
          <input type="hidden" name="javax.faces.ViewState" value="j_id9" />
          {(fresca ? """<input type="hidden" name="tela" value="fresca" />""" : "")}
          <select name="form0:j_id54"><option value="EM_FILA">Em fila</option><option value="ALTA">Alta</option></select>
          <input name="form0:dtInicialSolicitacaoInputDate" value="" />
          <input name="form0:dtFinalSolicitacaoInputDate" value="" />
          <input type="submit" id="form0:j_id75" name="form0:j_id75" value="Pesquisar" />
          <span id="form0:msgErro">{(totalCapado is { } t ? $"Consulta muito ampla, retorno limitado em 100 resultados. Total de resultados encontrados: {t}" : "")}</span>
          <table id="form0:listagem">
            <thead><tr><th>ID</th><th>Tipo</th><th>Recurso</th><th>Data da Solicitação</th><th>Paciente</th></tr></thead>
            <tbody>{string.Concat(ids.Select(i => $"<tr><td>{i}</td><td>CONSULTA</td><td>X</td><td>01/05/2025</td><td>P</td></tr>"))}</tbody>
          </table>
          <table id="form0:sc1_table"><tr>{string.Concat(Enumerable.Range(1, paginas).Select(p => $"<td>{p}</td>"))}</tr></table>
        </form></body></html>
        """;

    [Fact]
    public async Task Pesquisa_depois_de_janela_capada_traz_a_pagina_1_da_janela_nova()
    {
        var sessao = new SernitFalso(new Dictionary<string, (string[] Pagina1, int Paginas, int? Capado)>
        {
            ["13/04/2025"] = (["900001", "900002"], 5, 343),   // janela larga: capada
            ["14/04/2025"] = (["100001", "100002"], 5, null),  // janela menor: cabe
        });
        var leitor = new SernitLeitorService(sessao, NullLogger<SernitLeitorService>.Instance);

        var capada = await leitor.PesquisarAsync(Filtro("13/04/2025"), CancellationToken.None);
        capada.Capada.Should().BeTrue();

        var menor = await leitor.PesquisarAsync(Filtro("14/04/2025"), CancellationToken.None);

        menor.Linhas.Select(l => l.IdSernit).Should().BeEquivalentTo(["100001", "100002"],
            "a página 1 tem de ser a da janela pesquisada, não a da pesquisa anterior");
        sessao.TelasAbertas.Should().Be(2, "cada pesquisa parte de uma tela reaberta");
    }

    private static SernitFiltroPesquisa Filtro(string inicio) => new()
    {
        Situacao = SituacaoSernit.EmFila,
        DataSolicitacaoInicio = DateOnly.ParseExact(inicio, "dd/MM/yyyy"),
        DataSolicitacaoFim = DateOnly.ParseExact("05/01/2026", "dd/MM/yyyy"),
    };

    /// <summary>
    /// Imita o SERNIT: a janela é identificada pela data inicial; se o form enviado NÃO veio de uma tela
    /// reaberta, a página 1 da resposta é a da pesquisa anterior (o comportamento medido).
    /// </summary>
    private sealed class SernitFalso(Dictionary<string, (string[] Pagina1, int Paginas, int? Capado)> janelas) : ISernitWebSessao
    {
        private string[] _pagina1Anterior = [];
        public int TelasAbertas { get; private set; }

        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken)
        {
            TelasAbertas++;
            return Task.FromResult(Pagina(fresca: true, [], 0, null));
        }

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken)
        {
            var janela = janelas[extras[CampoInicio]];
            var formFresco = htmlForm.Contains("value=\"fresca\"", StringComparison.Ordinal);
            var pagina1 = formFresco ? janela.Pagina1 : _pagina1Anterior;
            _pagina1Anterior = janela.Pagina1;
            return Task.FromResult(Pagina(fresca: false, pagina1, janela.Paginas, janela.Capado));
        }

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RespostaSernit> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RespostaSernit(Encoding.UTF8.GetBytes(""), "text/html", null, null));

        public Task<RespostaSernit> SubmeterEscritaAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
            string operacao, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("teste de leitura não escreve");

        public Task<string> AutenticarAvulsoAsync(string usuario, string senha, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void UsarCredencialDoOperador(string usuario, string senha) => throw new NotSupportedException();

        public void Reiniciar() { }

        public Task<string?> SeguirRedirectNoCorpoAsync(string corpo, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
