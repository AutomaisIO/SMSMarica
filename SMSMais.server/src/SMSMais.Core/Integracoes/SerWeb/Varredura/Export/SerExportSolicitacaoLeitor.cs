using System.Globalization;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Integracoes.SerWeb.Varredura.Export;

/// <summary>
/// Leitura em lote pela tela <b>Lançamento → Solicitar Consulta/Exame</b>
/// (<c>solicitar-consulta-pesquisar.seam</c>), usando o <c>Exportar</c> dela.
///
/// <para><b>Existe por uma razão só: ALTA.</b> O combo da tela de Histórico — a que devolve 500 e
/// avisa quando corta — <b>não oferece a situação ALTA</b>. Ela só pode ser lida aqui.</para>
///
/// <para><b>Por que export e não paginação</b> (medido em 07/08/2026): esta tela corta em 100 e o
/// arquivo <b>não escapa desse teto</b> — exporta exatamente os mesmos 100 registros que a grade
/// mostraria. O ganho não é cobertura, é <b>uma requisição no lugar de cinco</b>: a leitura
/// paginada tinha dois caminhos de perda silenciosa (o laço que parava calado quando uma página
/// vinha vazia, e a contagem de páginas que devolvia zero quando o paginador não era encontrado no
/// HTML). Foi por ali que a varredura de 07/08 perdeu <b>853 registros de ALTA declarando
/// cobertura completa</b> — a detecção do teto funcionou, quem perdeu foi o trajeto.</para>
///
/// <para><b>O que esta tela dá de melhor</b> que a de Histórico: <c>CPF</c>, <c>Solicitante</c> e
/// <c>Município Solicitante</c> em toda linha — e foi por essas colunas que se provou que ela é
/// escopada pela credencial do operador (as 100 linhas de ALTA vieram todas com
/// <c>GESTOR SMS MARICA</c> / <c>MARICA</c>). Por isso aqui não há amarração de autocomplete a
/// fazer: a tela não tem campo de solicitante, e o recorte vem do login.</para>
///
/// <para><b>O que ela dá de pior:</b> nenhum aviso escrito de truncamento. Lote que volta com o
/// teto cheio é tratado como cortado — inferência conservadora: no máximo fatiamos à toa; o que
/// não pode acontecer é o contrário.</para>
/// </summary>
public interface ISerExportSolicitacaoLeitor : ISerExportLeitor;

public sealed class SerExportSolicitacaoLeitor(
    ISerWebSessao sessao,
    ILogger<SerExportSolicitacaoLeitor> logger) : ISerExportSolicitacaoLeitor
{
    public const string CaminhoTela =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    private const string CampoTipo = "form0:comboTipoRecurso";
    private const string CampoDataInicio = "form0:dtInicialSolicitacaoInputDate";
    private const string CampoDataFim = "form0:dtFinalSolicitacaoInputDate";

    /// <summary>5 páginas × 20 linhas. Medido em 07/08/2026: o arquivo respeita o mesmo teto —
    /// ALTA sem filtro devolveu exatamente 100 registros, EM_FILA idem.</summary>
    public int TetoPorLote => 100;

    public string Tela => "de Solicitação";

    private string _html = string.Empty;
    private string? _viewState;

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        var html = await sessao.AbrirTelaAsync(CaminhoTela, cancellationToken);

        // Sem o botão Pesquisar não é a tela de pesquisa — quase sempre é a de login, devolvida
        // com HTTP 200 quando outro login do mesmo operador derrubou a sessão. Reter a página do
        // lote anterior aqui faria a busca seguinte sair da conversa Seam velha.
        if (SerHtmlParser.BotaoPesquisar(SerHtmlParser.Documento(html)) is null)
        {
            throw new InvalidOperationException(
                "O GET da tela de Solicitação do SER não devolveu a tela de pesquisa (sem botão "
                + "Pesquisar). "
                + "Nada foi lido.");
        }

        _html = html;
        var vs = SerHtmlParser.ViewStateQualquer(html);
        if (!string.IsNullOrEmpty(vs)) _viewState = vs;
    }

    public async Task<LoteExportSer> ExportarAsync(
        SerFiltroExport filtro, CancellationToken cancellationToken)
    {
        // GET novo a cada busca, como na tela de Histórico: encadear submits a partir da página de
        // resultado reaproveita a conversa Seam anterior e o SER passa a devolver conjuntos
        // instáveis — medido lá, nunca medido aqui, e não é hipótese que se queira testar em
        // produção com a base do Estado.
        await PrepararAsync(cancellationToken);
        var doc = SerHtmlParser.Documento(_html);

        var botaoPesquisar = SerHtmlParser.BotaoPesquisar(doc)
            ?? throw new InvalidOperationException(
                "Botão Pesquisar não encontrado na tela de Solicitação do SER nem após reabri-la.");

        var filtros = MontarFiltros(doc, filtro);

        var resposta = await sessao.SubmeterFormAsync(
            _html,
            SerHtmlParser.FormPesquisa,
            new Dictionary<string, string>(filtros, StringComparer.Ordinal)
            {
                [botaoPesquisar] = botaoPesquisar,
                ["AJAXREQUEST"] = SerHtmlParser.FormPesquisa,
            },
            _viewState,
            cancellationToken);

        if (resposta.EhPlanilha)
        {
            throw new InvalidOperationException(
                "A pesquisa da tela de Solicitação devolveu uma planilha — o SER trocou os botões "
                + "de lugar. Nada foi lido.");
        }

        // Diferença importante em relação à tela de Histórico: AQUI a busca devolve a grade no
        // próprio corpo (medido em 07/08/2026 — sem `<meta name="Location">`). O redirect A4J
        // ainda é seguido se aparecer, para o dia em que a SES-RJ mudar isso.
        var htmlResultado = resposta.Texto;
        if (SerHtmlParser.RedirectNoCorpo(htmlResultado) is { Length: > 0 } destino)
        {
            htmlResultado = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }

        var docResultado = SerHtmlParser.Documento(htmlResultado);
        var mensagens = SerHtmlParser.Mensagens(docResultado);
        var linhasNaGrade = SerHtmlParser.LerGrade(docResultado).Count;

        // Esta tela não tem aviso de corte, então mensagem aqui é sempre coisa nova. Quando vem
        // COM resultado, é informativa e vira log; quando vem SEM nenhuma linha, é recusa do SER
        // se passando por "recorte vazio" — e recorte vazio silencioso já custou 35% de uma base.
        if (mensagens.Count > 0)
        {
            if (linhasNaGrade == 0)
            {
                throw new ValidacaoException(
                    "ser.consulta_recusada",
                    $"O SER recusou a consulta na tela de Solicitação: \"{mensagens[0]}\"");
            }

            logger.LogWarning(
                "SER/export-solicitacao: o SER mandou mensagem junto com o resultado: {Mensagem}",
                string.Join(" | ", mensagens));
        }

        if (linhasNaGrade == 0)
        {
            logger.LogDebug(
                "SER/export-solicitacao: {Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} não "
                + "devolveu linhas.",
                filtro.Situacao, filtro.DataSolicitacaoInicio, filtro.DataSolicitacaoFim);
            return new LoteExportSer([], Truncado: false);
        }

        var botaoExportar = SerHtmlParser.BotaoExportar(docResultado)
            ?? throw new InvalidOperationException(
                "A tela de Solicitação devolveu resultados mas não expôs o link Exportar. Layout "
                + "mudou — e sem o arquivo só restaria a paginação, que perde registro em silêncio.");

        // `jsfcljs` (commandLink do Mojarra): POST comum, SEM AJAXREQUEST. Os filtros vão de novo
        // junto, para o caso de o SER refazer a consulta em vez de reusar o resultado da conversa.
        var viewStateResultado = SerHtmlParser.ViewStateQualquer(htmlResultado) ?? _viewState;
        var arquivo = await sessao.SubmeterFormAsync(
            htmlResultado,
            SerHtmlParser.FormPesquisa,
            new Dictionary<string, string>(filtros, StringComparer.Ordinal)
            {
                [botaoExportar] = botaoExportar,
            },
            viewStateResultado,
            cancellationToken);

        if (!arquivo.EhPlanilha)
        {
            throw new InvalidOperationException(
                "O SER respondeu HTML no lugar da planilha ao exportar a tela de Solicitação "
                + $"({arquivo.Corpo.Length} bytes, content-type '{arquivo.ContentType}'). "
                + "A sessão é reautenticada sozinha quando o SER devolve a tela de login; se "
                + "chegou aqui, a tela veio diferente do esperado.");
        }

        // "Agendado para" É DESCARTADO desta tela — a coluna não é parseável (incidente 08/08/2026).
        //
        // O export a quebra em várias linhas do arquivo (`data`, `-`, `UNIDADE`) formando um FLUXO
        // que NÃO se alinha com as linhas de registro: os 100 registros ocupam as 100 primeiras
        // linhas e os fragmentos vêm todos depois, num bloco. Só o 1º registro fica com o próprio
        // valor certo; do 2º em diante a célula da linha pertence a outro agendamento. E o número
        // de linhas por registro é variável (1 quando não há agendamento, 3 quando há data +
        // unidade), então não dá para remontar contando — a informação de qual fragmento é de quem
        // simplesmente não está no arquivo.
        //
        // A primeira tentativa de remontar grudou fragmentos alheios no registro anterior,
        // estourou o `varchar(300)` de `agendado_para_texto` e derrubou a varredura em produção.
        // Dado errado é pior que dado ausente: aqui ele fica nulo, e quem preenche a coluna é a
        // tela de Histórico, que traz "Data do agendamento" em campo próprio e alinhado.
        var linhas = PlanilhaSerParser.Ler(arquivo.Corpo)
            .Select(l => l with { AgendadoPara = null })
            .ToList();

        // SEM aviso escrito, o único sinal é o lote vir com o teto cheio. É inferência — 100
        // exatos podem ser o total real —, mas na direção segura: no máximo o varredor fatia à
        // toa. Concluir "completo" de um lote cheio é que seria a perda invisível.
        var truncado = linhas.Count >= TetoPorLote;

        logger.LogInformation(
            "SER/export-solicitacao: {Situacao}{Tipo} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy} → "
            + "{Linhas} linhas{Corte}.",
            filtro.Situacao,
            filtro.Tipo is { } t ? $"/{t}" : string.Empty,
            filtro.DataSolicitacaoInicio, filtro.DataSolicitacaoFim,
            linhas.Count,
            truncado ? $" (LOTE CHEIO — teto de {TetoPorLote}, pode haver mais)" : string.Empty);

        var datas = linhas
            .Select(l => ParseData(l.DataSolicitacao))
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToList();

        return new LoteExportSer(linhas, truncado)
        {
            MaiorDataSolicitacao = datas.Count > 0 ? datas.Max() : null,
        };
    }

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Campos do recorte. O combo de situação é localizado pelo <c>&lt;select&gt;</c> que oferece
    /// <c>ALTA</c> — que é justamente o que distingue esta tela da de Histórico, e é estável a uma
    /// recompilação da página (ao contrário do <c>j_id75</c> lido numa captura).
    /// </summary>
    private static Dictionary<string, string> MontarFiltros(
        AngleSharp.Html.Dom.IHtmlDocument doc, SerFiltroExport filtro)
    {
        var campoSituacao = SerHtmlParser.SelectComOpcao(doc, SerHtmlParser.FormPesquisa, "ALTA")
            ?? throw new InvalidOperationException(
                "Não encontrei o combo de Situação na tela de Solicitação do SER (nenhum <select> "
                + "oferece a opção ALTA). Sem ele a consulta volta vazia.");

        var campos = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [campoSituacao] = SerCodigos.Codigo(filtro.Situacao),
            [CampoDataInicio] = Br(filtro.DataSolicitacaoInicio),
            [CampoDataFim] = Br(filtro.DataSolicitacaoFim),
        };

        // Aqui o filtro de Tipo FUNCIONA — medido em 07/08/2026: ALTA sem filtro devolve 37
        // consultas + 63 exames; com CONSULTA devolve 100 consultas, conjunto diferente. Na tela
        // de Histórico esse mesmo combo é decorativo. Como o corte por Tipo é o último recurso da
        // bisecção quando um dia sozinho estoura, essa diferença é o que a torna confiável aqui.
        if (filtro.Tipo is { } tipo) campos[CampoTipo] = SerCodigos.Codigo(tipo);

        // `UnidadeSolicitante` é ignorado de propósito: esta tela não tem o campo, e o recorte de
        // Maricá vem da credencial do operador — provado pelas colunas Solicitante e Município
        // Solicitante, que vieram "GESTOR SMS MARICA"/"MARICA" em 100 de 100 linhas de ALTA.
        return campos;
    }

    private static DateOnly? ParseData(string? texto) =>
        DateOnly.TryParseExact(texto?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

    private static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
