using System.Globalization;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
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
    /// <summary>
    /// Quantos registros a tela devolve por lote antes de cortar.
    ///
    /// <para>Existe porque a varredura roda sobre <b>duas</b> telas com tetos diferentes — 500 na
    /// de Histórico, 100 na de Solicitação (a única com ALTA) — e o varredor precisa saber quando
    /// a janela está folgada o bastante para crescer.</para>
    /// </summary>
    int TetoPorLote { get; }

    /// <summary>Nome da tela, para as mensagens de fatia truncada dizerem de onde veio o corte.</summary>
    string Tela { get; }

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

    /// <summary>Teto declarado pela própria tela ("retorno limitado em 500 resultados"). Aqui ele
    /// é só a régua de folga do varredor: quem decide se o lote foi cortado é o AVISO, nunca a
    /// contagem — 500 exatos podem ser o total real.</summary>
    public int TetoPorLote => 500;

    public string Tela => "de Histórico";

    /// <summary>Container A4J default (<c>A4J.AJAX.VIEW_ROOT_ID</c>). É o que o navegador manda em
    /// <c>AJAXREQUEST</c> nas requisições do suggestionbox — o init do componente não passa
    /// <c>containerId</c>, então o framework cai no view root. Extraído do
    /// <c>framework.pack.js</c> servido pelo próprio SER (07/08/2026).</summary>
    private const string RegiaoViewRoot = "_viewRoot";

    /// <summary>Último HTML COMPLETO da tela (fonte dos campos do submit).</summary>
    private string _html = string.Empty;
    private string? _viewState;

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        var html = await sessao.AbrirTelaAsync(CaminhoTela, cancellationToken);

        // Se o GET não devolveu a tela de pesquisa, FALHA — reter a página do lote anterior em
        // silêncio quebraria a regra do "GET novo por busca" (docs/ser.md §4.3) sem ninguém ver:
        // a busca sairia da conversa velha e o resultado voltaria instável.
        if (SerHtmlParser.BotaoPesquisar(SerHtmlParser.Documento(html)) is null)
        {
            throw new InvalidOperationException(
                "O GET da tela de Histórico do SER não devolveu a tela de pesquisa (sem botão "
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
        // SEMPRE uma tela nova antes de pesquisar. Reusar a página de RESULTADO como base do
        // submit seguinte reaproveita o `?cid` da conversa Seam anterior — e aí o SER devolve
        // conjuntos INSTÁVEIS: medido em 07/08/2026, três buscas idênticas com GET novo dão
        // resultado idêntico, enquanto encadeadas pelo resultado anterior variam entre si. É a
        // mesma família da armadilha do `action` (docs/ser.md §3.3): o que muda o resultado não é
        // o filtro, é de onde se posta. Custa um GET por lote e compra determinismo.
        await PrepararAsync(cancellationToken);
        var doc = SerHtmlParser.Documento(_html);

        var botaoPesquisar = SerHtmlParser.BotaoPesquisar(doc)
            ?? throw new InvalidOperationException(
                "Botão Pesquisar não encontrado na tela de Histórico do SER nem após reabri-la "
                + "(a página foi recompilada pela SES-RJ?).");

        var filtros = MontarFiltros(doc, filtro);

        // O filtro de Solicitante NÃO liga por texto: o SER amarra a unidade no servidor durante
        // a ida-e-volta A4J do autocomplete, e só então a busca sai recortada. Sem essa amarração
        // o export lê a fila do ESTADO INTEIRO — inclusive PII de pacientes de outros municípios —
        // parecendo "aleatório" entre chamadas. Medido em 07/08/2026 (docs/ser.md §4.3).
        var viewState = _viewState;
        var solicitanteAmarrado = false;
        if (!string.IsNullOrWhiteSpace(filtro.UnidadeSolicitante))
        {
            viewState = await AmarrarSolicitanteAsync(
                filtro.UnidadeSolicitante.Trim(), filtros, viewState, cancellationToken);
            solicitanteAmarrado = true;
        }
        else
        {
            // Legítimo quando alguém pede o Estado inteiro DE PROPÓSITO — mas nunca sem rastro:
            // foi exatamente essa leitura, sem ninguém perceber, que alimentou a base até 07/08.
            logger.LogWarning(
                "SER/export: consulta SEM filtro de solicitante — o recorte é o ESTADO INTEIRO "
                + "({Situacao} {Inicio:dd/MM/yyyy}..{Fim:dd/MM/yyyy}).",
                filtro.Situacao, filtro.DataSolicitacaoInicio, filtro.DataSolicitacaoFim);
        }

        var extras = new Dictionary<string, string>(filtros, StringComparer.Ordinal)
        {
            [botaoPesquisar] = botaoPesquisar,
            // Com o solicitante amarrado, a busca replica o navegador por inteiro (a sequência
            // provada na sonda usa _viewRoot nas três requisições). Sem solicitante, mantém o
            // form0 que a varredura sempre usou.
            ["AJAXREQUEST"] = solicitanteAmarrado ? RegiaoViewRoot : SerHtmlParser.FormPesquisa,
        };

        var resposta = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extras, viewState, cancellationToken);

        if (resposta.EhPlanilha)
        {
            throw new InvalidOperationException(
                "A pesquisa da tela de Histórico devolveu uma planilha — o SER trocou os botões de "
                + "lugar. Nada foi lido.");
        }

        // A BUSCA NÃO DEVOLVE O RESULTADO: devolve um redirect A4J de ~267 bytes com
        // <meta name="Location"> apontando para a mesma tela com o ?cid da conversa Seam. O
        // resultado só existe na página de destino.
        //
        // Medido no SER em 06/08/2026: sem seguir o redirect, o parser lê os 267 bytes, não acha
        // grade nem mensagem, e devolve "zero linhas, sem aviso de corte" — que a tela traduzia
        // como "recorte completo". Silêncio virando prova de cobertura, de novo. É o mesmo
        // mecanismo que a tela de histórico já usava (docs/ser.md §5); só faltava aqui.
        var htmlResultado = resposta.Texto;

        if (SerHtmlParser.RedirectNoCorpo(htmlResultado) is { Length: > 0 } destino)
        {
            htmlResultado = await sessao.AbrirTelaAsync(destino, cancellationToken);
        }
        else
        {
            // FALHA DURA, não warning. A ausência do redirect é quase sempre a sessão derrubada
            // por outro login: o SER responde a tela de LOGIN com HTTP 200, que não tem grade nem
            // mensagens — seguir adiante viraria "lote vazio, sem aviso de corte", o varredor
            // aplicaria o vazio e AVANÇARIA o cursor por cima de um recorte nunca lido. É a mesma
            // classe de perda invisível dos ~35%; o leg do export já tratava a queda como erro
            // duro (EhPlanilha), o da busca deixava passar. Achado do repasse de 07/08/2026.
            throw new InvalidOperationException(
                "A busca da tela de Histórico do SER não devolveu o redirect A4J "
                + $"({resposta.Corpo.Length} bytes). Costuma ser a sessão derrubada por outro "
                + "login do mesmo operador. Nada foi lido — o recorte fica para a retomada.");
        }

        // NÃO absorve: a página de resultado serve para ESTE export e morre aqui. Promovê-la a
        // fonte dos submits seguintes é o que tornava o resultado instável.
        var viewStateResultado = SerHtmlParser.ViewStateQualquer(htmlResultado) ?? viewState;
        var docResultado = SerHtmlParser.Documento(htmlResultado);

        var aviso = SerHtmlParser.AvisoDeLimite(docResultado);
        var truncado = aviso is not null;

        // O SER RECUSA a consulta por escrito quando algo está errado — p.ex. "Para listar todos
        // os tipos de situação, é necessário informar um dos campos: nome do paciente, código,
        // CNS, CPF ou ID da solicitação". Sem tratar isso, a recusa chega aqui como "zero linhas,
        // sem aviso de corte" e vira "recorte completo": silêncio virando prova de cobertura, que
        // é exatamente o erro que custou 35% da base. Qualquer mensagem que não seja o aviso de
        // limite é falha, e sobe com as palavras do próprio SER.
        var recusa = SerHtmlParser.Mensagens(docResultado)
            .FirstOrDefault(m => !string.Equals(m, aviso, StringComparison.Ordinal));

        if (recusa is not null)
        {
            throw new ValidacaoException(
                "ser.consulta_recusada",
                $"O SER recusou a consulta: \"{recusa}\"");
        }

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
        // de situação/tipo/data vão de novo junto — mas o SOLICITANTE não se re-amarra por texto:
        // o que garante o recorte da planilha é a amarração feita antes da busca NESTA MESMA
        // conversa Seam, e por isso o POST sai do action da página de resultado (com o `cid` que
        // produziu o lote). Medido em 07/08: export da rodada amarrada abre no mesmo 1º registro
        // da grade.
        var extrasExport = new Dictionary<string, string>(filtros, StringComparer.Ordinal)
        {
            [botaoExportar] = botaoExportar,
        };

        var arquivo = await sessao.SubmeterFormAsync(
            htmlResultado, SerHtmlParser.FormPesquisa, extrasExport, viewStateResultado,
            cancellationToken);

        if (!arquivo.EhPlanilha)
        {
            // Sessão morta devolve a tela de login com HTTP 200 no lugar da planilha. Desde
            // 10/08/2026 a SerWebSessao detecta isso e reautentica sozinha, então chegar aqui
            // significa outra coisa — layout mudou, ou o SER está fora.
            throw new InvalidOperationException(
                "O SER respondeu HTML no lugar da planilha ao exportar "
                + $"({arquivo.Corpo.Length} bytes, content-type '{arquivo.ContentType}'). "
                + "A sessão é reautenticada sozinha quando o SER devolve a tela de login; se "
                + "chegou aqui, a tela veio diferente do esperado.");
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

    /// <summary>
    /// Reproduz a ida-e-volta do <c>rich:suggestionbox</c> que amarra a unidade solicitante na
    /// conversa Seam — o que o operador faz sem perceber ao digitar e <b>clicar na sugestão</b>.
    ///
    /// <para>Duas requisições, com o protocolo lido do <c>ui.pack.js</c>/<c>framework.pack.js</c>
    /// servidos pelo próprio SER (07/08/2026):</para>
    ///
    /// <list type="number">
    /// <item><b>Fetch de sugestões</b> — o texto vai no parâmetro <c>inputvalue</c> (default do
    /// RichFaces; o init não o sobrescreve) + <c>ajaxSingle=&lt;box&gt;</c>. A resposta traz a
    /// tabela <c>&lt;box&gt;:suggest</c>, e a LISTA fica guardada na conversa do servidor.</item>
    /// <item><b>Onselect</b> — o hidden <c>&lt;box&gt;_selection</c> leva o <b>índice da linha
    /// escolhida</b> (o RichFaces o preenche só durante este submit e o limpa em seguida — por
    /// isso ele parecia "sempre vazio" e o filtro parecia não ter mecanismo). O servidor resolve
    /// o índice contra a lista guardada e grava a unidade na conversa.</item>
    /// </list>
    ///
    /// <para><b>Sem sugestão que case, a leitura FALHA</b> — nunca degrada para busca sem filtro,
    /// porque busca sem filtro aqui significa ler a fila do Estado inteiro, com PII de pacientes
    /// de outros municípios (medido em 07/08/2026; ver docs/ser.md §4.3).</para>
    /// </summary>
    /// <returns>O ViewState mais fresco após as duas respostas, para a busca que vem em seguida.</returns>
    private async Task<string?> AmarrarSolicitanteAsync(
        string solicitante,
        Dictionary<string, string> filtros,
        string? viewState,
        CancellationToken cancellationToken)
    {
        var caixa = SerHtmlParser.SuggestionBoxDoCampo(_html, CampoUnidadeSolicitante)
            ?? throw new InvalidOperationException(
                $"Não encontrei o script do autocomplete de Solicitante ({CampoUnidadeSolicitante}) "
                + "na tela de Histórico do SER. Sem ele o filtro não amarra e a consulta leria o "
                + "Estado inteiro — nada foi lido.");

        // --- 1) fetch de sugestões ---
        var extrasFetch = new Dictionary<string, string>(filtros, StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            ["inputvalue"] = solicitante,
            [caixa.BoxId] = caixa.BoxId,
            ["ajaxSingle"] = caixa.BoxId,
        };

        var respostaFetch = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extrasFetch, viewState, cancellationToken);

        var docFetch = SerHtmlParser.Documento(respostaFetch.Texto);
        var sugestoes = SerHtmlParser.LinhasDeSugestao(docFetch, caixa.BoxId)
            ?? throw new ValidacaoException(
                "ser.autocomplete_sem_resposta",
                "O autocomplete de Solicitante do SER não devolveu a tabela de sugestões — o "
                + "protocolo mudou? Sem a amarração a consulta leria o Estado inteiro; nada foi lido.");

        // O índice enviado no _selection é a POSIÇÃO DA LINHA na tabela, então o casamento é pela
        // primeira célula (o nome da unidade), nunca por "contains" no texto da linha inteira —
        // a linha repete o nome em outras colunas e um contains casaria a linha errada.
        var indice = -1;
        for (var i = 0; i < sugestoes.Count; i++)
        {
            var primeira = sugestoes[i].Count > 0 ? sugestoes[i][0] : string.Empty;
            if (string.Equals(primeira.Trim(), solicitante, StringComparison.OrdinalIgnoreCase))
            {
                indice = i;
                break;
            }
        }

        if (indice < 0)
        {
            var vistas = string.Join("; ", sugestoes.Select(s => string.Join(" | ", s)).Take(5));
            throw new ValidacaoException(
                "ser.solicitante_nao_resolvido",
                $"O SER não sugeriu \"{solicitante}\" no autocomplete de Solicitante "
                + $"(veio: {(vistas.Length > 0 ? vistas : "nada")}). Sem a amarração a consulta "
                + "leria o Estado inteiro; nada foi lido.");
        }

        viewState = SerHtmlParser.ViewStateQualquer(respostaFetch.Texto) ?? viewState;

        // --- 2) onselect, com o índice no hidden _selection ---
        var extrasSelect = new Dictionary<string, string>(filtros, StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = RegiaoViewRoot,
            [caixa.OnselectId] = caixa.OnselectId,
            ["ajaxSingle"] = caixa.BoxId,
            [caixa.CampoSelecao] = indice.ToString(CultureInfo.InvariantCulture),
        };

        var respostaSelect = await sessao.SubmeterFormAsync(
            _html, SerHtmlParser.FormPesquisa, extrasSelect, viewState, cancellationToken);

        // A resposta boa do onselect é um envelope A4J (<meta name="Ajax-Response">, medido na
        // sonda de 07/08). A tela de login — sessão derrubada — não o tem, e o efeito do onselect
        // é INVISÍVEL (estado na conversa Seam): sem esta checagem, um onselect que não rodou
        // deixaria a busca sair sem filtro com cara de filtrada — o Estado inteiro importado como
        // recorte de Maricá. Achado do repasse de 07/08/2026.
        if (!respostaSelect.Texto.Contains("name=\"Ajax-Response\"", StringComparison.Ordinal))
        {
            throw new ValidacaoException(
                "ser.amarracao_sem_confirmacao",
                "O onselect do autocomplete de Solicitante não devolveu o envelope A4J — a "
                + "amarração não aconteceu (sessão derrubada?). Buscar assim leria o Estado "
                + "inteiro; nada foi lido.");
        }

        logger.LogDebug(
            "SER/export: solicitante \"{Solicitante}\" amarrado pelo autocomplete (índice {Indice}).",
            solicitante, indice);

        return SerHtmlParser.ViewStateQualquer(respostaSelect.Texto) ?? viewState;
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
        };

        // O texto do solicitante vai junto (o navegador também o manda), mas quem FILTRA é a
        // amarração feita em AmarrarSolicitanteAsync — texto sozinho é decorativo e a consulta
        // sai do Estado inteiro (medido em 07/08/2026).
        if (!string.IsNullOrWhiteSpace(filtro.UnidadeSolicitante))
        {
            campos[CampoUnidadeSolicitante] = filtro.UnidadeSolicitante.Trim();
        }

        if (filtro.Tipo is { } tipo) campos[CampoTipo] = SerCodigos.Codigo(tipo);

        return campos;
    }

    private static DateOnly? ParseData(string? texto) =>
        DateOnly.TryParseExact(texto?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

    private static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
