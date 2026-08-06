using System.Globalization;
using Microsoft.Extensions.Logging;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura.Export;

/// <summary>
/// Leitura em lote pela tela <b>Consulta → Histórico de Consulta/Exame</b>
/// (<c>historico-pesquisar.seam</c>), que exporta o resultado inteiro num <c>.xls</c>.
///
/// <para><b>Por que esta tela e não a de Solicitação:</b> a de Solicitação pagina de 20 em 20 e
/// para em 5 páginas (100 registros), corta em silêncio, e foi assim que a carga inicial perdeu
/// ~35% da base. Esta aqui devolve <b>500 por lote num arquivo só</b> e <b>avisa por escrito</b>
/// quando trunca — o que transforma "cobertura" de suposição em fato verificável.</para>
///
/// <para><b>O que se perde na troca:</b> esta tela não tem CPF, Solicitante nem Município
/// solicitante nas colunas, e o combo de situação <b>não tem ALTA</b>. Por isso ALTA continua sendo
/// varrida pela tela de Solicitação, e o CPF continua vindo da fase de histórico.</para>
/// </summary>
public interface ISerExportLeitor
{
    Task PrepararAsync(CancellationToken cancellationToken);

    /// <summary>Pesquisa a fatia e baixa a planilha. Duas requisições por lote.</summary>
    Task<LoteExportSer> ExportarAsync(SerFiltroExport filtro, CancellationToken cancellationToken);
}

public sealed class SerExportLeitor(
    ISerWebSessao sessao,
    ILogger<SerExportLeitor> logger) : ISerExportLeitor
{
    /// <summary>Tela de Histórico de Consulta/Exame — outro caminho, mesma conversa Seam.</summary>
    public const string CaminhoTela =
        "/ser/pages/historico/consulta-exame/solicitacao/historico-pesquisar.seam";

    private const string CampoTipo = "form0:tipo";
    private const string CampoDataInicio = "form0:dataInicialInputDate";
    private const string CampoDataFim = "form0:dataFinalInputDate";
    private const string CampoUnidadeSolicitante = "form0:suggUnidadeSol";

    /// <summary>Último HTML COMPLETO da tela (fonte dos campos do submit).</summary>
    private string _html = string.Empty;
    private string? _viewState;

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        var html = await sessao.AbrirTelaAsync(CaminhoTela, cancellationToken);
        Absorver(html);
    }

    public async Task<LoteExportSer> ExportarAsync(
        SerFiltroExport filtro, CancellationToken cancellationToken)
    {
        var doc = await GarantirTelaAsync(cancellationToken);

        var botaoPesquisar = SerHtmlParser.BotaoPesquisar(doc)
            ?? throw new InvalidOperationException(
                "Botão Pesquisar não encontrado na tela de Histórico do SER nem após reabri-la "
                + "(a página foi recompilada pela SES-RJ?).");

        var filtros = MontarFiltros(doc, filtro);

        var extras = new Dictionary<string, string>(filtros, StringComparer.Ordinal)
        {
            [botaoPesquisar] = botaoPesquisar,
            ["AJAXREQUEST"] = SerHtmlParser.FormPesquisa,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extras, _viewState, cancellationToken);

        if (resposta.EhPlanilha)
        {
            throw new InvalidOperationException(
                "A pesquisa da tela de Histórico devolveu uma planilha — o SER trocou os botões de "
                + "lugar. Nada foi lido.");
        }

        var htmlResultado = resposta.Texto;
        Absorver(htmlResultado);
        var docResultado = SerHtmlParser.Documento(htmlResultado);

        var aviso = SerHtmlParser.AvisoDeLimite(docResultado);
        var truncado = aviso is not null;

        // Sem linha nenhuma na grade não há o que exportar — e a planilha vazia do SER só custaria
        // mais uma requisição e um arquivo sem cabeçalho reconhecível.
        if (SerHtmlParser.LerGrade(docResultado).Count == 0)
        {
            logger.LogDebug(
                "SER/export: {Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} não devolveu linhas.",
                filtro.Situacao, filtro.DataSolicitacaoInicio, filtro.DataSolicitacaoFim);
            return new LoteExportSer([], truncado) { Aviso = aviso };
        }

        var botaoExportar = SerHtmlParser.BotaoExportar(docResultado)
            ?? throw new InvalidOperationException(
                "A tela de Histórico devolveu resultados mas não expôs o link Exportar. "
                + "Layout mudou — não dá para garantir cobertura por paginação nesta tela.");

        // O Exportar é `jsfcljs` (commandLink do Mojarra): POST comum, SEM AJAXREQUEST. Os filtros
        // vão de novo junto — se o SER refizer a consulta em vez de reaproveitar o resultado da
        // conversa, a planilha ainda sai do recorte certo.
        var extrasExport = new Dictionary<string, string>(filtros, StringComparer.Ordinal)
        {
            [botaoExportar] = botaoExportar,
        };

        var arquivo = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extrasExport, _viewState, cancellationToken);

        if (!arquivo.EhPlanilha)
        {
            // Quase sempre é a sessão do SER derrubada por outro login do mesmo operador: em vez da
            // planilha vem a tela de login, com HTTP 200.
            throw new InvalidOperationException(
                "O SER respondeu HTML no lugar da planilha ao exportar "
                + $"({arquivo.Corpo.Length} bytes, content-type '{arquivo.ContentType}'). "
                + "Costuma ser a sessão derrubada por outro login do mesmo operador.");
        }

        var linhas = PlanilhaSerParser.Ler(arquivo.Corpo);

        logger.LogInformation(
            "SER/export: {Situacao}{Tipo} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} → {Linhas} linhas"
            + "{Corte}.",
            filtro.Situacao,
            filtro.Tipo is { } t ? $"/{t}" : string.Empty,
            filtro.DataSolicitacaoInicio, filtro.DataSolicitacaoFim,
            linhas.Count,
            truncado ? " (SER AVISOU CORTE EM 500)" : string.Empty);

        var datas = linhas
            .Select(l => ParseData(l.DataSolicitacao))
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToList();

        return new LoteExportSer(linhas, truncado)
        {
            MaiorDataSolicitacao = datas.Count > 0 ? datas.Max() : null,
            Aviso = aviso,
        };
    }

    // ------------------------------------------------------------------ interno

    private async Task<AngleSharp.Html.Dom.IHtmlDocument> GarantirTelaAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_html)) await PrepararAsync(cancellationToken);

        var doc = SerHtmlParser.Documento(_html);
        if (SerHtmlParser.BotaoPesquisar(doc) is not null) return doc;

        // Perdemos a tela (navegação para outra view, ou o Seam expirou a conversa). Reabrir custa
        // um GET e evita que a rodada inteira morra — foi o que aconteceu na carga inicial.
        logger.LogInformation("SER/export: tela de Histórico perdida — reabrindo.");
        await PrepararAsync(cancellationToken);
        return SerHtmlParser.Documento(_html);
    }

    /// <summary>
    /// Campos do recorte, usados nas DUAS requisições do lote.
    ///
    /// <para>A situação é localizada pelo <c>&lt;select&gt;</c> que oferece a opção <c>EM_FILA</c>,
    /// e não pelo id <c>form0:j_id57</c> lido numa captura: <c>j_id</c> é posicional e muda quando a
    /// SES-RJ recompila a página — e um combo errado devolveria outra listagem sem erro nenhum.</para>
    /// </summary>
    private static Dictionary<string, string> MontarFiltros(
        AngleSharp.Html.Dom.IHtmlDocument doc, SerFiltroExport filtro)
    {
        var campoSituacao = SerHtmlParser.SelectComOpcao(doc, SerHtmlParser.FormPesquisa, "EM_FILA")
            ?? throw new InvalidOperationException(
                "Não encontrei o combo de Situação na tela de Histórico do SER (nenhum <select> "
                + "oferece a opção EM_FILA). Sem ele a consulta volta vazia.");

        if (filtro.Situacao == SituacaoSer.Alta)
        {
            // O combo desta tela tem 6 situações e ALTA não é uma delas. Mandar assim mesmo faria o
            // JSF cair no valor default e devolver OUTRA situação como se fosse a pedida.
            throw new InvalidOperationException(
                "A tela de Histórico do SER não oferece a situação ALTA. Varra ALTA pela tela de "
                + "Solicitação.");
        }

        var campos = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [campoSituacao] = SerCodigos.Codigo(filtro.Situacao),
            [CampoDataInicio] = Br(filtro.DataSolicitacaoInicio),
            [CampoDataFim] = Br(filtro.DataSolicitacaoFim),
            [CampoUnidadeSolicitante] = filtro.UnidadeSolicitante,
        };

        if (filtro.Tipo is { } tipo) campos[CampoTipo] = SerCodigos.Codigo(tipo);

        return campos;
    }

    private void Absorver(string html)
    {
        // Mesma regra da tela de Solicitação: só é "página de formulário" quem tem o botão de
        // pesquisa. A resposta A4J pode vir parcial, e promovê-la a base dos submits seguintes
        // quebra tudo o que vem depois (docs/ser.md §3.4).
        if (SerHtmlParser.BotaoPesquisar(SerHtmlParser.Documento(html)) is not null) _html = html;

        var vs = SerHtmlParser.ViewStateQualquer(html);
        if (!string.IsNullOrEmpty(vs)) _viewState = vs;
    }

    private static DateOnly? ParseData(string? texto) =>
        DateOnly.TryParseExact(texto?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

    private static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
