using AngleSharp.Html.Dom;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Testes da varredura por export do SER.
///
/// <para>O que está sob teste aqui é <b>cobertura</b>: a pergunta que custou ~35% da base foi
/// "existe registro que o varredor nunca pediu?". O SER falso abaixo tem uma população conhecida e
/// aplica o mesmo corte de 500 da tela real, então dá para afirmar, e não supor, que toda
/// solicitação foi lida.</para>
/// </summary>
public class VarredorSerPorExportTests
{
    private static VarredorSerPorExport Varredor(SerFalso ser) =>
        new(ser, NullLogger<VarredorSerPorExport>.Instance);

    /// <summary>Base pequena, cabe num lote só: uma requisição e nada de fatiar.</summary>
    [Fact]
    public async Task Le_tudo_num_lote_so_quando_cabe()
    {
        var ser = new SerFalso(PopularPorDia(new DateOnly(2026, 1, 1), dias: 10, porDia: 3));

        var lidas = new List<SerLinhaGrade>();
        var resultado = await Varredor(ser).VarrerAsync(
            SituacaoSer.EmFila, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10),
            (linhas, _, _) => { lidas.AddRange(linhas); return Task.CompletedTask; },
            CancellationToken.None);

        lidas.Should().HaveCount(30);
        resultado.Buscas.Should().Be(1);
        resultado.Truncadas.Should().BeEmpty();
    }

    /// <summary>
    /// O caso que importa: a janela inteira estoura os 500 e o varredor precisa encolher até
    /// caber. <b>Nenhum ID pode faltar</b> — é a garantia que a paginação de 20 em 20 não dava.
    /// </summary>
    [Fact]
    public async Task Nao_perde_ninguem_quando_o_ser_corta_em_500()
    {
        // 60 dias × 40 por dia = 2.400 registros, muito acima do teto de 500.
        var populacao = PopularPorDia(new DateOnly(2026, 1, 1), dias: 60, porDia: 40);
        var ser = new SerFalso(populacao);

        var lidas = new List<SerLinhaGrade>();
        var resultado = await Varredor(ser).VarrerAsync(
            SituacaoSer.EmFila, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1),
            (linhas, _, _) => { lidas.AddRange(linhas); return Task.CompletedTask; },
            CancellationToken.None);

        lidas.Select(l => l.IdSer).Distinct()
            .Should().BeEquivalentTo(populacao.Select(p => p.IdSer).Distinct());
        resultado.Truncadas.Should().BeEmpty();

        // O corte precisa ter acontecido de verdade, senão o teste passaria sem exercitar nada:
        // o varredor RECEBE lote cortado (é assim que ele descobre que a janela é grande demais),
        // encolhe a janela e refaz — o que ele nunca pode fazer é APLICAR o lote cortado, e é isso
        // que a igualdade de IDs acima prova.
        ser.LotesEntregues.Should().Contain(l => l.Truncado);
    }

    /// <summary>
    /// Volume concentrado num único dia: não há janela menor que 1 dia, então só resta fatiar por
    /// Tipo. Com CONSULTA e EXAME cabendo separados, a cobertura continua completa.
    /// </summary>
    [Fact]
    public async Task Fatia_por_tipo_quando_um_dia_so_estoura()
    {
        var dia = new DateOnly(2026, 2, 10);
        var populacao = new List<LinhaFalsa>();
        populacao.AddRange(Popular(dia, 400, TipoRecursoSer.Consulta, inicioId: 1_000_000));
        populacao.AddRange(Popular(dia, 400, TipoRecursoSer.Exame, inicioId: 2_000_000));

        var ser = new SerFalso(populacao);

        var lidas = new List<SerLinhaGrade>();
        var resultado = await Varredor(ser).VarrerAsync(
            SituacaoSer.EmFila, dia, dia,
            (linhas, _, _) => { lidas.AddRange(linhas); return Task.CompletedTask; },
            CancellationToken.None);

        lidas.Select(l => l.IdSer).Distinct().Should().HaveCount(800);
        resultado.Truncadas.Should().BeEmpty();
        ser.FiltrosPedidos.Should().Contain(f => f.Tipo == TipoRecursoSer.Consulta);
        ser.FiltrosPedidos.Should().Contain(f => f.Tipo == TipoRecursoSer.Exame);
    }

    /// <summary>
    /// Um dia + um tipo que ainda passa de 500 é registro <b>não lido</b>. Tem que virar fatia
    /// truncada declarada — a rodada vira Parcial, nunca Concluída. Fingir cobertura aqui é o que
    /// faz o operador confiar numa base furada.
    /// </summary>
    [Fact]
    public async Task Declara_fatia_truncada_quando_nem_o_tipo_resolve()
    {
        var dia = new DateOnly(2026, 2, 10);
        var ser = new SerFalso(Popular(dia, 700, TipoRecursoSer.Consulta, inicioId: 1_000_000));

        var resultado = await Varredor(ser).VarrerAsync(
            SituacaoSer.EmFila, dia, dia,
            (_, _, _) => Task.CompletedTask,
            CancellationToken.None);

        resultado.Truncadas.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Situacao = SituacaoSer.EmFila, Dia = dia, Tipo = TipoRecursoSer.Consulta });
    }

    /// <summary>
    /// O cursor é a promessa "tudo antes desta data já está no banco". Ele só pode avançar depois
    /// que o lote foi aplicado, e nunca pode andar para trás — senão a retomada pula um trecho.
    /// </summary>
    [Fact]
    public async Task Cursor_avanca_monotonicamente_e_so_depois_de_aplicar()
    {
        var ser = new SerFalso(PopularPorDia(new DateOnly(2026, 1, 1), dias: 40, porDia: 30));

        var cursores = new List<DateOnly>();
        var aplicadasAteOCursor = 0;

        await Varredor(ser).VarrerAsync(
            SituacaoSer.EmFila, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 9),
            (linhas, cursor, _) =>
            {
                aplicadasAteOCursor += linhas.Count;
                cursores.Add(cursor);

                // Tudo que tem data ANTES do cursor já precisa ter sido entregue neste ponto.
                var deveriamEstar = ser.Populacao.Count(p => p.Data < cursor);
                aplicadasAteOCursor.Should().BeGreaterThanOrEqualTo(deveriamEstar);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        cursores.Should().BeInAscendingOrder();
        cursores[^1].Should().BeOnOrAfter(new DateOnly(2026, 2, 10));
    }

    /// <summary>A tela de Histórico não tem ALTA no combo — pedir ALTA por ela tem que explodir,
    /// não devolver a situação errada calada.</summary>
    [Fact]
    public async Task Recusa_situacao_alta_que_a_tela_de_export_nao_oferece()
    {
        var leitor = new SerExportLeitor(new SessaoFalsa(), NullLogger<SerExportLeitor>.Instance);

        var acao = async () => await leitor.ExportarAsync(
            new SerFiltroExport
            {
                Situacao = SituacaoSer.Alta,
                DataSolicitacaoInicio = new DateOnly(2026, 1, 1),
                DataSolicitacaoFim = new DateOnly(2026, 1, 31),
            },
            CancellationToken.None);

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ALTA*");
    }

    // ------------------------------------------------------------------ fakes

    private sealed record LinhaFalsa(string IdSer, DateOnly Data, TipoRecursoSer Tipo);

    private static List<LinhaFalsa> Popular(
        DateOnly dia, int quantidade, TipoRecursoSer tipo, int inicioId) =>
        Enumerable.Range(0, quantidade)
            .Select(i => new LinhaFalsa((inicioId + i).ToString(), dia, tipo))
            .ToList();

    private static List<LinhaFalsa> PopularPorDia(DateOnly inicio, int dias, int porDia)
    {
        var linhas = new List<LinhaFalsa>();
        var id = 1_000_000;
        for (var d = 0; d < dias; d++)
        {
            var dia = inicio.AddDays(d);
            for (var i = 0; i < porDia; i++)
            {
                linhas.Add(new LinhaFalsa(
                    (id++).ToString(), dia, i % 2 == 0 ? TipoRecursoSer.Consulta : TipoRecursoSer.Exame));
            }
        }
        return linhas;
    }

    /// <summary>
    /// SER falso com o comportamento que importa: filtra pelo recorte pedido e <b>corta em 500
    /// avisando</b>, exatamente como a tela de Histórico.
    ///
    /// <para>O corte é feito em ordem <b>arbitrária</b> (embaralhada por id), de propósito: a
    /// ordenação do export nunca foi provada, e um varredor que só funcione se o SER cortar
    /// ordenado por data seria correto no teste e furado em produção.</para>
    /// </summary>
    private sealed class SerFalso(List<LinhaFalsa> populacao) : ISerExportLeitor
    {
        private const int Teto = 500;

        public List<LinhaFalsa> Populacao { get; } = populacao;
        public List<SerFiltroExport> FiltrosPedidos { get; } = [];
        public List<LoteExportSer> LotesEntregues { get; } = [];

        public Task PrepararAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<LoteExportSer> ExportarAsync(
            SerFiltroExport filtro, CancellationToken cancellationToken)
        {
            FiltrosPedidos.Add(filtro);

            var casam = Populacao
                .Where(p => p.Data >= filtro.DataSolicitacaoInicio && p.Data <= filtro.DataSolicitacaoFim)
                .Where(p => filtro.Tipo is null || p.Tipo == filtro.Tipo)
                .OrderBy(p => p.IdSer.GetHashCode())
                .ToList();

            var truncado = casam.Count > Teto;
            var linhas = casam
                .Take(Teto)
                .Select(p => new SerLinhaGrade
                {
                    IdSer = p.IdSer,
                    DataSolicitacao = p.Data.ToString("dd/MM/yyyy"),
                    Tipo = p.Tipo.ToString().ToUpperInvariant(),
                })
                .ToList();

            var lote = new LoteExportSer(linhas, truncado);
            LotesEntregues.Add(lote);
            return Task.FromResult(lote);
        }
    }

    /// <summary>Sessão que nunca é usada: o teste de ALTA falha antes de qualquer requisição.</summary>
    private sealed class SessaoFalsa : ISerWebSessao
    {
        private const string TelaMinima = """
            <html><body><form id="form0" action="/ser/x.seam">
              <a id="form0:btnSearch" title="Pesquisar"><span>Pesquisar</span></a>
              <select name="form0:j_id57"><option value="EM_FILA">Em fila</option></select>
            </form></body></html>
            """;

        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) =>
            Task.FromResult(TelaMinima);

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) =>
            Task.FromResult(TelaMinima);

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("não deveria chegar na rede");

        public Task<RespostaSer> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("não deveria chegar na rede");

        public Task<string> AutenticarAvulsoAsync(
            string usuario, string senha, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Reiniciar() { }
    }
}
