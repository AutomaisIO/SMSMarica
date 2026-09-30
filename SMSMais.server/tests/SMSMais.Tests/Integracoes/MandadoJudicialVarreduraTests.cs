using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Integracoes.SerWeb.Varredura.Export;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Fase Judicial da varredura do SER e do SERNIT (Indicadores de Regulação, 30/09/2026).
///
/// <para>A grade dos dois sistemas não mostra o martelo: só o checkbox "Somente com mandado judicial" da
/// pesquisa separa quem tem mandado. O que está sob teste: achar o checkbox pelo RÓTULO (o j_id muda),
/// nunca deixar o filtro vazar para uma pesquisa comum (checkbox vazio ainda liga o filtro no JSF), juntar
/// os IDs e só declarar a leitura completa quando nenhuma fatia estourou o teto — é isso que autoriza
/// carimbar "verificado em" na base. HTML sintético, sem dado real.</para>
/// </summary>
public class MandadoJudicialVarreduraTests
{
    // ------------------------------------------------------------------ checkbox pelo rótulo

    private const string FormComDoisCheckboxes = """
        <html><body>
        <form id="form0" action="/ser/pages/x.seam">
          <input type="hidden" name="form0" value="form0" />
          <strong>Solicitação vermelha</strong>
          <input type="checkbox" name="form0:j_id80" />
          <strong>Somente urgentes</strong>
          <input type="checkbox" name="form0:j_id87" />
          <input type="hidden" name="form0:j_id88" value="" />
          <strong>Somente com mandado&nbsp;judicial</strong>
          <input type="submit" name="form0:j_id90" value="Pesquisar" />
        </form></body></html>
        """;

    [Fact]
    public void Ser_acha_o_checkbox_pelo_strong_que_vem_depois_dele()
    {
        var doc = SerHtmlParser.Documento(FormComDoisCheckboxes);

        SerHtmlParser.CheckboxMandadoJudicial(doc, "form0").Should().Be("form0:j_id87",
            "o rótulo de mandado vem logo depois do segundo checkbox; o primeiro tem o próprio rótulo");
    }

    [Fact]
    public void Sernit_acha_o_checkbox_pelo_strong_que_vem_depois_dele()
    {
        var doc = SernitHtmlParser.Documento(FormComDoisCheckboxes);

        SernitHtmlParser.CheckboxMandadoJudicial(doc, "form0").Should().Be("form0:j_id87");
    }

    [Fact]
    public void Rotulo_de_mandado_escrito_antes_de_outro_checkbox_nao_casa_com_o_anterior()
    {
        // O primeiro checkbox tem o rótulo "Outro filtro"; o texto de mandado aparece depois, sem checkbox
        // à frente. Casar com o primeiro faria uma pesquisa comum virar "só judiciais".
        const string html = """
            <html><body><form id="form0" action="/x">
              <input type="checkbox" name="form0:a" /><strong>Outro filtro</strong>
              <strong>Somente com mandado judicial</strong>
            </form></body></html>
            """;

        SerHtmlParser.CheckboxMandadoJudicial(SerHtmlParser.Documento(html), "form0").Should().BeNull();
        SernitHtmlParser.CheckboxMandadoJudicial(SernitHtmlParser.Documento(html), "form0").Should().BeNull();
    }

    // ------------------------------------------------------------------ a chave sai do POST

    [Fact]
    public void Sentinela_tira_do_post_o_checkbox_que_o_form_renderizou_ligado()
    {
        // Resposta de uma pesquisa judicial: o form volta com o checkbox MARCADO. A pesquisa comum seguinte,
        // partindo dele, levaria a chave junto — e o SERNIT/SER devolveriam só os judiciais.
        const string html = """
            <html><body><form id="form0" action="/x">
              <input type="checkbox" name="form0:j_id66" checked="checked" /><strong>Somente com mandado judicial</strong>
              <input name="form0:dtInicialSolicitacaoInputDate" value="01/01/2025" />
            </form></body></html>
            """;

        var sernit = SernitHtmlParser.CamposDoForm(SernitHtmlParser.Documento(html), "form0");
        sernit.Should().ContainKey("form0:j_id66", "o form renderizou o checkbox ligado");
        SernitHtmlParser.AplicarExtras(sernit, new Dictionary<string, string>
        {
            ["form0:j_id66"] = SernitHtmlParser.RemoverDoPost,
            ["form0:dtInicialSolicitacaoInputDate"] = "02/02/2025",
        });
        sernit.Should().NotContainKey("form0:j_id66");
        sernit["form0:dtInicialSolicitacaoInputDate"].Should().Be("02/02/2025");

        var ser = SerHtmlParser.CamposDoForm(SerHtmlParser.Documento(html), "form0");
        SerHtmlParser.AplicarExtras(ser, new Dictionary<string, string> { ["form0:j_id66"] = SerHtmlParser.RemoverDoPost });
        ser.Should().NotContainKey("form0:j_id66");
    }

    // ------------------------------------------------------------------ SER: tela de Solicitação

    private const string TelaSolicitacaoSer = """
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
          <input type="hidden" name="form0" value="form0" />
          <select name="form0:j_id75"><option value="EM_FILA">Em fila</option><option value="ALTA">Alta</option></select>
          <input name="form0:dtInicialSolicitacaoInputDate" value="" />
          <input name="form0:dtFinalSolicitacaoInputDate" value="" />
          <input type="checkbox" name="form0:j_id87" /><strong>Somente com mandado judicial</strong>
          <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
        </form></body></html>
        """;

    private const string SemResultado = """
        <html><body><form id="form0" action="/ser/x.seam">
          <a class="rf-btn" id="form0:j_id96" title="Pesquisar"><span>Pesquisar</span></a>
          <ul id="form0:messages"></ul>
        </form></body></html>
        """;

    private static SerFiltroExport FiltroSer(bool judicial) => new()
    {
        Situacao = SituacaoSer.Agendada,
        DataSolicitacaoInicio = new DateOnly(2010, 1, 1),
        DataSolicitacaoFim = new DateOnly(2026, 10, 1),
        MandadoJudicial = judicial,
    };

    [Fact]
    public async Task Ser_liga_o_checkbox_so_quando_pedido_e_tira_a_chave_quando_nao()
    {
        var ligado = new SessaoSerFalsa(TelaSolicitacaoSer, SemResultado);
        await new SerExportSolicitacaoLeitor(ligado, NullLogger<SerExportSolicitacaoLeitor>.Instance)
            .ExportarAsync(FiltroSer(judicial: true), CancellationToken.None);
        ligado.Submits[0]["form0:j_id87"].Should().Be("on");

        var desligado = new SessaoSerFalsa(TelaSolicitacaoSer, SemResultado);
        await new SerExportSolicitacaoLeitor(desligado, NullLogger<SerExportSolicitacaoLeitor>.Instance)
            .ExportarAsync(FiltroSer(judicial: false), CancellationToken.None);
        desligado.Submits[0]["form0:j_id87"].Should().Be(SerHtmlParser.RemoverDoPost,
            "desligado, a chave tem de SAIR do POST — vazia ainda ligaria o filtro");
    }

    [Fact]
    public async Task Ser_sem_checkbox_na_tela_falha_alto_em_vez_de_trazer_a_fila_inteira()
    {
        var semCheckbox = TelaSolicitacaoSer.Replace(
            """<input type="checkbox" name="form0:j_id87" /><strong>Somente com mandado judicial</strong>""", "");
        var leitor = new SerExportSolicitacaoLeitor(
            new SessaoSerFalsa(semCheckbox, SemResultado), NullLogger<SerExportSolicitacaoLeitor>.Instance);

        var acao = () => leitor.ExportarAsync(FiltroSer(judicial: true), CancellationToken.None);

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*mandado judicial*");
    }

    // ------------------------------------------------------------------ SERNIT: pesquisa

    private const string TelaSernit = """
        <html><body>
        <form id="form0" action="/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam">
          <input type="hidden" name="form0" value="form0" />
          <select name="form0:j_id54"><option value="EM_FILA">Em fila</option></select>
          <input name="form0:dtInicialSolicitacaoInputDate" value="" />
          <input name="form0:dtFinalSolicitacaoInputDate" value="" />
          <input type="checkbox" name="form0:j_id66" /><strong>Somente com mandado judicial</strong>
          <input type="submit" id="form0:j_id75" name="form0:j_id75" value="Pesquisar" />
          <table id="form0:listagem"><tbody></tbody></table>
        </form></body></html>
        """;

    [Fact]
    public async Task Sernit_liga_o_checkbox_so_quando_pedido_e_tira_a_chave_quando_nao()
    {
        var sessao = new SessaoSernitFalsa(TelaSernit);
        var leitor = new SernitLeitorService(sessao, NullLogger<SernitLeitorService>.Instance);

        await leitor.PesquisarAsync(
            new SernitFiltroPesquisa { Situacao = SituacaoSernit.EmFila, MandadoJudicial = true }, CancellationToken.None);
        await leitor.PesquisarAsync(
            new SernitFiltroPesquisa { Situacao = SituacaoSernit.EmFila }, CancellationToken.None);

        sessao.Pesquisas[0]["form0:j_id66"].Should().Be("on");
        sessao.Pesquisas[1]["form0:j_id66"].Should().Be(SernitHtmlParser.RemoverDoPost,
            "a pesquisa comum depois da judicial não pode herdar o filtro");
    }

    // ------------------------------------------------------------------ leitura da fase

    [Fact]
    public async Task Ser_junta_os_ids_de_todas_as_situacoes_com_o_filtro_ligado_e_completa()
    {
        var leitor = new LeitorSerFalso(
            (f, _) => f.Situacao switch
            {
                SituacaoSer.Agendada => ["7001", "7002"],
                SituacaoSer.Alta => ["7002", "7003"],
                _ => [],
            });
        var varredor = new VarredorSerPorExport(NullLogger<VarredorSerPorExport>.Instance);

        var leitura = await MandadoJudicialSer.LerAsync(
            leitor, varredor, [SituacaoSer.EmFila, SituacaoSer.Agendada, SituacaoSer.Alta],
            new DateOnly(2026, 10, 1), CancellationToken.None);

        leitura.Ids.Should().BeEquivalentTo(["7001", "7002", "7003"]);
        leitura.Completa.Should().BeTrue("nenhum lote veio cheio — pode carimbar a base como verificada");
        leitor.Filtros.Should().OnlyContain(f => f.MandadoJudicial, "toda busca da fase leva o filtro");
        leitor.Filtros.Should().OnlyContain(f => f.DataSolicitacaoInicio == MandadoJudicialSer.InicioJanela);
    }

    [Fact]
    public async Task Ser_com_fatia_truncada_nao_e_completa_mas_guarda_os_ids_lidos()
    {
        var diaCheio = new DateOnly(2024, 5, 5);
        var leitor = new LeitorSerFalso(
            (f, _) => f.Situacao == SituacaoSer.ChegadaConfirmada ? ["8001"] : [],
            truncado: f => f.Situacao == SituacaoSer.ChegadaConfirmada
                           && f.DataSolicitacaoInicio <= diaCheio && diaCheio <= f.DataSolicitacaoFim);
        var varredor = new VarredorSerPorExport(NullLogger<VarredorSerPorExport>.Instance);

        var leitura = await MandadoJudicialSer.LerAsync(
            leitor, varredor, [SituacaoSer.ChegadaConfirmada], new DateOnly(2026, 10, 1), CancellationToken.None);

        leitura.Completa.Should().BeFalse("um dia estourou o teto mesmo por Tipo — há judiciais não lidos");
        leitura.Resultado.Truncadas.Should().OnlyContain(t => t.Dia == diaCheio);
        leitura.Ids.Should().Contain("8001", "o que foi lido é real e é marcado mesmo sem o carimbo");
    }

    [Fact]
    public async Task Sernit_junta_os_ids_com_o_filtro_ligado_e_completa()
    {
        var leitor = new LeitorSernitFalso(
            f => f.Situacao == SituacaoSernit.Agendada ? ["501", "502"] : []);
        var varredor = new VarredorSernitPorPaginacao(leitor, NullLogger<VarredorSernitPorPaginacao>.Instance);

        var leitura = await MandadoJudicialSernit.LerAsync(
            varredor, [SituacaoSernit.EmFila, SituacaoSernit.Agendada], new DateOnly(2026, 10, 1), CancellationToken.None);

        leitura.Ids.Should().BeEquivalentTo(["501", "502"]);
        leitura.Completa.Should().BeTrue();
        leitor.Filtros.Should().OnlyContain(f => f.MandadoJudicial);
    }

    [Fact]
    public async Task Sernit_zero_judiciais_tambem_e_leitura_completa()
    {
        // Medido em 30/09/2026: zero para Maricá, com controle. Zero é resposta — e carimba a base.
        var leitor = new LeitorSernitFalso(_ => []);
        var varredor = new VarredorSernitPorPaginacao(leitor, NullLogger<VarredorSernitPorPaginacao>.Instance);

        var leitura = await MandadoJudicialSernit.LerAsync(
            varredor, [SituacaoSernit.EmFila, SituacaoSernit.Alta], new DateOnly(2026, 10, 1), CancellationToken.None);

        leitura.Ids.Should().BeEmpty();
        leitura.Completa.Should().BeTrue();
    }

    // ------------------------------------------------------------------ dublês

    private sealed class LeitorSerFalso(
        Func<SerFiltroExport, int, string[]> ids, Func<SerFiltroExport, bool>? truncado = null) : ISerExportLeitor
    {
        public List<SerFiltroExport> Filtros { get; } = [];
        public int TetoPorLote => 100;
        public string Tela => "de Solicitação";
        public Task PrepararAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<LoteExportSer> ExportarAsync(SerFiltroExport filtro, CancellationToken cancellationToken)
        {
            Filtros.Add(filtro);
            var linhas = ids(filtro, Filtros.Count).Select(i => new SerLinhaGrade { IdSer = i }).ToList();
            return Task.FromResult(new LoteExportSer(linhas, truncado?.Invoke(filtro) ?? false));
        }
    }

    private sealed class LeitorSernitFalso(Func<SernitFiltroPesquisa, string[]> ids) : ISernitLeitorService
    {
        public List<SernitFiltroPesquisa> Filtros { get; } = [];

        public Task PrepararAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SernitPaginaGrade> PesquisarAsync(SernitFiltroPesquisa filtro, CancellationToken cancellationToken)
        {
            Filtros.Add(filtro);
            var linhas = ids(filtro).Select(i => new SernitLinhaGrade { IdSernit = i }).ToList();
            return Task.FromResult(new SernitPaginaGrade(linhas, 1, linhas.Count, Capada: false));
        }

        public Task<IReadOnlyList<SernitLinhaGrade>> IrParaPaginaAsync(int pagina, CancellationToken cancellationToken) =>
            throw new NotSupportedException("uma página só");

        public Task<SernitHistorico> AbrirHistoricoAsync(int indiceNaPagina, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SernitHistorico> LerHistoricoPorIdAsync(
            string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> RegistrarFollowUpAsync(
            string idSernit, SituacaoSernit situacao, string texto, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a fase Judicial só lê");

        public Task<SernitContatosDaTela> LerContatosAsync(
            string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SernitContatosDaTela> AlterarContatosAsync(
            string idSernit, SituacaoSernit situacao, IReadOnlyDictionary<string, string> novos,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("a fase Judicial só lê");
    }

    private sealed class SessaoSerFalsa(string tela, params string[] respostas) : ISerWebSessao
    {
        private readonly Queue<string> _respostas = new(respostas);
        public List<Dictionary<string, string>> Submits { get; } = [];

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) => Task.FromResult(tela);

        public Task<RespostaSer> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, CancellationToken cancellationToken)
        {
            Submits.Add(new Dictionary<string, string>(extras, StringComparer.Ordinal));
            var corpo = _respostas.Count > 0 ? _respostas.Dequeue() : "<html></html>";
            return Task.FromResult(new RespostaSer(Encoding.UTF8.GetBytes(corpo), "text/html", null, null));
        }

        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> AutenticarAvulsoAsync(string usuario, string senha, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RespostaSer> SubmeterEscritaAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, string operacao, CancellationToken cancellationToken) =>
            throw new NotSupportedException($"dublê de leitura recebeu escrita: {operacao}");

        public void UsarCredencialDoOperador(string usuario, string senha) => throw new NotSupportedException();

        public void Reiniciar() { }
    }

    private sealed class SessaoSernitFalsa(string tela) : ISernitWebSessao
    {
        public List<Dictionary<string, string>> Pesquisas { get; } = [];

        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) => Task.FromResult(tela);

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken)
        {
            Pesquisas.Add(new Dictionary<string, string>(extras, StringComparer.Ordinal));
            return Task.FromResult(tela);
        }

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RespostaSernit> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken) => throw new NotSupportedException();

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
