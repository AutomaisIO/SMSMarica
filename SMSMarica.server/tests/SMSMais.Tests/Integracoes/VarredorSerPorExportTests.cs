using AngleSharp.Html.Dom;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura.Export;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Tests.Integracoes;

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
    /// <summary>O leitor não entra mais no construtor: o varredor serve as DUAS telas do SER
    /// (Histórico, teto 500 com aviso escrito; Solicitação, teto 100 sem aviso — a única com
    /// ALTA), e quem escolhe é o chamador, por situação.</summary>
    private static VarredorSerPorExport Varredor() =>
        new(NullLogger<VarredorSerPorExport>.Instance);

    /// <summary>Base pequena, cabe num lote só: uma requisição e nada de fatiar.</summary>
    [Fact]
    public async Task Le_tudo_num_lote_so_quando_cabe()
    {
        var ser = new SerFalso(PopularPorDia(new DateOnly(2026, 1, 1), dias: 10, porDia: 3));

        var lidas = new List<SerLinhaGrade>();
        var resultado = await Varredor().VarrerAsync(ser,
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
        var resultado = await Varredor().VarrerAsync(ser,
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
        var resultado = await Varredor().VarrerAsync(ser,
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

        var resultado = await Varredor().VarrerAsync(ser,
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

        await Varredor().VarrerAsync(ser,
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

    // ------------------------------------------------------------------ ALTA (tela de 100)

    /// <summary>
    /// ALTA vem da tela de Solicitação, que corta em 100 e <b>não avisa</b>. É o recorte que em
    /// 07/08/2026 perdeu 853 registros em produção declarando cobertura completa: nenhum ID pode
    /// faltar, e a rodada não pode terminar sem nada declarado.
    /// </summary>
    [Fact]
    public async Task Alta_nao_perde_ninguem_com_o_teto_de_100_sem_aviso()
    {
        // 3.300 registros: a ordem de grandeza real da ALTA de Maricá (3.307 no SER em 08/08).
        var populacao = PopularPorDia(new DateOnly(2020, 1, 1), dias: 330, porDia: 10);
        var ser = new SerFalso(populacao, teto: 100, avisaPorEscrito: false);

        var lidas = new List<SerLinhaGrade>();
        var resultado = await Varredor().VarrerAsync(ser,
            SituacaoSer.Alta, new DateOnly(2020, 1, 1), new DateOnly(2020, 11, 25),
            (linhas, _, _) => { lidas.AddRange(linhas); return Task.CompletedTask; },
            CancellationToken.None);

        lidas.Select(l => l.IdSer).Distinct()
            .Should().BeEquivalentTo(populacao.Select(p => p.IdSer).Distinct());
        resultado.Truncadas.Should().BeEmpty();
        ser.LotesEntregues.Should().Contain(l => l.Truncado, "o teto precisa ter sido exercitado");
    }

    /// <summary>
    /// Sem aviso escrito, um lote que volta CHEIO é indistinguível de um recorte que por acaso tem
    /// exatamente o teto. O varredor tem que tratar como cortado — errar para o lado de fatiar à
    /// toa é barato; errar para o lado de concluir cobertura é o que custou a base.
    /// </summary>
    [Fact]
    public async Task Alta_trata_lote_exatamente_no_teto_como_cortado()
    {
        var dia = new DateOnly(2020, 5, 10);
        var ser = new SerFalso(
            Popular(dia, 100, TipoRecursoSer.Consulta, inicioId: 900_000),
            teto: 100, avisaPorEscrito: false);

        var resultado = await Varredor().VarrerAsync(ser,
            SituacaoSer.Alta, dia, dia,
            (_, _, _) => Task.CompletedTask,
            CancellationToken.None);

        resultado.Truncadas.Should().ContainSingle()
            .Which.Tipo.Should().Be(TipoRecursoSer.Consulta);
    }

    /// <summary>
    /// Perto do fim do intervalo a janela já vem grudada em <c>fim</c>. Partir ao meio um passo
    /// grande que não encosta nela repetia o MESMO lote cortado várias vezes contra a produção do
    /// Estado sem trazer nada novo — e com teto de 100 esse desperdício é 5× mais frequente.
    /// </summary>
    [Fact]
    public async Task Encolhimento_nao_repete_o_mesmo_recorte_no_fim_da_janela()
    {
        var inicio = new DateOnly(2020, 1, 1);
        var fim = new DateOnly(2020, 1, 3);
        var ser = new SerFalso(PopularPorDia(inicio, dias: 3, porDia: 60), teto: 100, avisaPorEscrito: false);

        await Varredor().VarrerAsync(ser,
            SituacaoSer.Alta, inicio, fim,
            (_, _, _) => Task.CompletedTask,
            CancellationToken.None);

        var repetidos = ser.FiltrosPedidos
            .GroupBy(f => (f.DataSolicitacaoInicio, f.DataSolicitacaoFim, f.Tipo))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        repetidos.Should().BeEmpty("nenhum recorte idêntico pode ser pedido duas vezes ao SER");
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
    private sealed class SerFalso(List<LinhaFalsa> populacao, int teto = 500, bool avisaPorEscrito = true)
        : ISerExportLeitor
    {
        public int TetoPorLote { get; } = teto;

        public string Tela => avisaPorEscrito ? "de Histórico" : "de Solicitação";

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

            // Duas telas, dois jeitos de saber que cortou. A de Histórico AVISA por escrito — e
            // por isso sabe distinguir "exatamente o teto" de "mais que o teto". A de Solicitação
            // não avisa nada: o único sinal é o lote voltar cheio, então lote no teto é tratado
            // como cortado mesmo quando por acaso era o total exato.
            var truncado = avisaPorEscrito
                ? casam.Count > TetoPorLote
                : casam.Count >= TetoPorLote;

            var linhas = casam
                .Take(TetoPorLote)
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

        /// <summary>Estes testes exercitam LEITURA. Se algum caminho chamar a escrita, é bug do
        /// código sob teste — falhar alto aqui é o que denuncia.</summary>
        public Task<RespostaSer> SubmeterEscritaAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, string operacao, CancellationToken cancellationToken) =>
            throw new NotSupportedException($"dublê de leitura recebeu escrita: {operacao}");

        public void UsarCredencialDoOperador(string usuario, string senha) =>
            throw new NotSupportedException("dublê de leitura não autentica operador");


        public void Reiniciar() { }
    }
}
