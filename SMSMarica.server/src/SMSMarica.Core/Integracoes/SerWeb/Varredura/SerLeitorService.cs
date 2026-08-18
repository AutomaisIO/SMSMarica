using System.Globalization;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Integracoes.SerWeb.Varredura;

/// <summary>
/// Leitura da fila do SER: pesquisa, paginação e histórico. É a camada que fala "SER" —
/// não conhece o banco. Quem persiste é o runner da varredura.
///
/// <para><b>Estado interno:</b> guarda DOIS HTMLs. O último <b>completo</b> (com
/// <c>&lt;form id="form0"&gt;</c>) é a fonte dos campos do submit; o último <b>recebido</b> é a
/// fonte das linhas. A resposta do <c>rich:datascroller</c> é parcial — traz a grade nova sem
/// form nenhum — e usá-la como base do próximo submit quebra (docs/ser.md §3.3).</para>
/// </summary>
public interface ISerLeitorService
{
    Task PrepararAsync(CancellationToken cancellationToken);

    Task<SerPaginaGrade> PesquisarAsync(SerFiltroPesquisa filtro, CancellationToken cancellationToken);

    Task<IReadOnlyList<SerLinhaGrade>> IrParaPaginaAsync(int pagina, CancellationToken cancellationToken);

    /// <summary>Abre o "Histórico da Solicitação" da linha indicada (índice 0-based NA PÁGINA).</summary>
    Task<SerHistorico> AbrirHistoricoAsync(int indiceNaPagina, CancellationToken cancellationToken);

    /// <summary>Ciclo otimizado de histórico por ID: pesquisa o ID exato (1 linha) e abre o
    /// histórico. Duas requisições — é o piso, porque abrir o histórico descarta a busca.</summary>
    Task<SerHistorico> LerHistoricoPorIdAsync(
        string idSer, SituacaoSer situacao, CancellationToken cancellationToken);

    /// <summary>
    /// Registra um FollowUP (observação) na solicitação. <b>Isto ESCREVE no SER</b> — protocolo
    /// em docs/ser.md §9. Devolve a mensagem que o SER exibiu, que <b>não é prova</b> de nada:
    /// quem confirma é a releitura do histórico.
    /// </summary>
    Task<string> RegistrarFollowUpAsync(
        string idSer, SituacaoSer situacao, string texto, CancellationToken cancellationToken);
}

/// <summary>A linha não oferece "Registrar FollowUP" no menu Opções (acontece na situação Alta).</summary>
public sealed class FollowUpSerIndisponivelException(string mensagem) : Exception(mensagem);

public sealed class SerLeitorService(
    ISerWebSessao sessao,
    ILogger<SerLeitorService> logger) : ISerLeitorService
{
    private const string CampoSituacao = "form0:j_id75";
    private const string CampoTipo = "form0:comboTipoRecurso";
    private const string CampoCpf = "form0:cpf";
    private const string CampoNome = "form0:nome";
    private const string CampoCns = "form0:cns";
    private const string CampoIdSolicitacao = "form0:idSolicitacao";
    private const string CampoDataInicio = "form0:dtInicialSolicitacaoInputDate";
    private const string CampoDataFim = "form0:dtFinalSolicitacaoInputDate";

    /// <summary>Último HTML COMPLETO (fonte dos campos do form).</summary>
    private string _htmlForm = string.Empty;

    /// <summary>Último HTML recebido (fonte das linhas). Pode ser parcial.</summary>
    private string _htmlDados = string.Empty;

    private string? _ultimoViewState;

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        var html = await sessao.AbrirTelaPesquisaAsync(cancellationToken);
        Absorver(html);
    }

    public async Task<SerPaginaGrade> PesquisarAsync(
        SerFiltroPesquisa filtro, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_htmlForm)) await PrepararAsync(cancellationToken);

        var doc = SerHtmlParser.Documento(_htmlForm);
        if (SerHtmlParser.BotaoPesquisar(doc) is null)
        {
            // Auto-recuperação: perdemos a tela de pesquisa (navegamos para outra view, ou o
            // Seam expirou a conversa). Reabrir custa um GET e evita que a rodada inteira morra
            // — foi assim que a carga inicial perdeu 10.501 históricos por um único desvio.
            logger.LogInformation("SER: tela de pesquisa perdida — reabrindo antes de pesquisar.");
            await PrepararAsync(cancellationToken);
            doc = SerHtmlParser.Documento(_htmlForm);
        }

        var botao = SerHtmlParser.BotaoPesquisar(doc)
            ?? throw new InvalidOperationException(
                "Botão Pesquisar não encontrado na tela do SER nem após reabri-la "
                + "(a página foi recompilada pela SES-RJ?).");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [botao] = botao,
            ["AJAXREQUEST"] = SerHtmlParser.FormPesquisa,
            // Situação é OBRIGATÓRIA no SER — pesquisar sem ela devolve zero.
            [CampoSituacao] = SerCodigos.Codigo(filtro.Situacao),
        };

        if (filtro.Tipo is { } tipo) extras[CampoTipo] = SerCodigos.Codigo(tipo);
        if (filtro.DataSolicitacaoInicio is { } di) extras[CampoDataInicio] = Br(di);
        if (filtro.DataSolicitacaoFim is { } df) extras[CampoDataFim] = Br(df);
        if (!string.IsNullOrWhiteSpace(filtro.Cpf)) extras[CampoCpf] = filtro.Cpf!;
        if (!string.IsNullOrWhiteSpace(filtro.Nome)) extras[CampoNome] = filtro.Nome!;
        if (!string.IsNullOrWhiteSpace(filtro.Cns)) extras[CampoCns] = filtro.Cns!;
        if (!string.IsNullOrWhiteSpace(filtro.IdSolicitacao)) extras[CampoIdSolicitacao] = filtro.IdSolicitacao!;

        var html = await sessao.SubmeterPesquisaAsync(_htmlForm, extras, _ultimoViewState, cancellationToken);
        Absorver(html);

        var respostaDoc = SerHtmlParser.Documento(_htmlDados);
        return new SerPaginaGrade(
            SerHtmlParser.LerGrade(respostaDoc),
            SerHtmlParser.PaginasNaResposta(respostaDoc));
    }

    public async Task<IReadOnlyList<SerLinhaGrade>> IrParaPaginaAsync(
        int pagina, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_htmlForm))
            throw new InvalidOperationException("Pesquise antes de paginar.");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = SerHtmlParser.FormPesquisa,
            ["ajaxSingle"] = SerHtmlParser.Scroller,
            [SerHtmlParser.Scroller] = pagina.ToString(CultureInfo.InvariantCulture),
        };

        // Estrutura vem do último HTML COMPLETO; o ViewState, da última resposta (parcial).
        var html = await sessao.SubmeterPesquisaAsync(_htmlForm, extras, _ultimoViewState, cancellationToken);
        Absorver(html);
        return SerHtmlParser.LerGrade(SerHtmlParser.Documento(_htmlDados));
    }

    public async Task<SerHistorico> AbrirHistoricoAsync(
        int indiceNaPagina, CancellationToken cancellationToken)
    {
        var dados = SerHtmlParser.Documento(_htmlDados);
        var item = SerHtmlParser.ItemHistorico(dados, indiceNaPagina);
        if (item is null)
        {
            var disponiveis = string.Join(", ", SerHtmlParser.ItensDeOpcoes(dados, indiceNaPagina).Keys);
            throw new HistoricoSerIndisponivelException(
                $"A linha {indiceNaPagina} não oferece 'Histórico da Solicitação' no menu Opções "
                + $"(solicitações em Alta não oferecem). Itens disponíveis: [{disponiveis}]");
        }

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [item] = item,
            ["AJAXREQUEST"] = SerHtmlParser.FormPesquisa,
        };

        var resposta = await sessao.SubmeterPesquisaAsync(
            _htmlForm, extras, _ultimoViewState, cancellationToken);

        // O item responde 200 com um XHTML minúsculo mandando redirecionar por <meta Location>.
        if (sessao is SerWebSessao concreta)
        {
            var destino = await concreta.SeguirRedirectNoCorpoAsync(resposta, cancellationToken);
            if (destino is not null) resposta = destino;
        }

        Absorver(resposta);
        return SerHtmlParser.LerHistorico(SerHtmlParser.Documento(resposta));
    }

    public async Task<SerHistorico> LerHistoricoPorIdAsync(
        string idSer, SituacaoSer situacao, CancellationToken cancellationToken)
    {
        var pagina = await PesquisarAsync(
            new SerFiltroPesquisa { Situacao = situacao, IdSolicitacao = idSer }, cancellationToken);

        if (pagina.Linhas.Count != 1)
        {
            throw new InvalidOperationException(
                $"A busca pelo ID {idSer} em {situacao} devolveu {pagina.Linhas.Count} linhas — "
                + "esperava exatamente 1. A solicitação pode ter mudado de situação.");
        }

        var historico = await AbrirHistoricoAsync(0, cancellationToken);

        // Conferência de identidade: o SER pode devolver a tela de outra solicitação se o
        // ViewState estiver defasado. Sem esta checagem, gravaríamos a trilha do paciente errado.
        var voltou = historico.IdSolicitacao;
        if (!string.IsNullOrWhiteSpace(voltou) && !string.Equals(voltou, idSer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O SER devolveu o histórico da solicitação {voltou} quando pedimos {idSer}. "
                + "Nada foi gravado.");
        }

        logger.LogDebug("SER: histórico de {IdSer} lido com {Eventos} eventos.", idSer, historico.Eventos.Count);
        return historico;
    }

    public async Task<string> RegistrarFollowUpAsync(
        string idSer, SituacaoSer situacao, string texto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ArgumentException("FollowUP sem texto não registra nada.", nameof(texto));
        }

        var pagina = await PesquisarAsync(
            new SerFiltroPesquisa { Situacao = situacao, IdSolicitacao = idSer }, cancellationToken);

        // Mesma guarda do histórico: sem 1 linha exata não dá para saber em QUEM escreveríamos.
        if (pagina.Linhas.Count != 1)
        {
            throw new InvalidOperationException(
                $"A busca pelo ID {idSer} em {situacao} devolveu {pagina.Linhas.Count} linhas — "
                + "esperava exatamente 1. Nada foi escrito no SER.");
        }

        var dados = SerHtmlParser.Documento(_htmlDados);
        var item = SerHtmlParser.ItemFollowUp(dados, 0);
        if (item is null)
        {
            var disponiveis = string.Join(", ", SerHtmlParser.ItensDeOpcoes(dados, 0).Keys);
            throw new FollowUpSerIndisponivelException(
                $"A solicitação {idSer} não oferece 'Registrar FollowUP' no menu Opções. "
                + $"Itens disponíveis: [{disponiveis}]");
        }

        // 1) Abrir o modal. Não grava nada — mas passa pela porta de escrita porque o RÓTULO do
        // item ("Registrar FollowUP") casa com a trava, e afrouxar a trava para deixar isto passar
        // abriria a porta para o `Cancelar` de qualquer tela.
        var abertura = await sessao.SubmeterEscritaAsync(
            _htmlForm,
            SerHtmlParser.FormPesquisa,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [item] = item,
                ["AJAXREQUEST"] = "_viewRoot",
            },
            _ultimoViewState,
            $"abrir modal de FollowUP da solicitação {idSer}",
            cancellationToken);

        var htmlModal = abertura.Texto;
        var modal = SerHtmlParser.ModalDeObservacao(SerHtmlParser.Documento(htmlModal))
            ?? throw new InvalidOperationException(
                "O SER não devolveu o modal de FollowUP (form com textarea + botão Gravar). "
                + "O layout mudou? Nada foi escrito.");

        logger.LogInformation(
            "SER: modal de FollowUP de {IdSer} — form {Form}, texto {Campo}, gravar {Botao}.",
            idSer, modal.FormId, modal.CampoTexto, modal.BotaoGravar);

        // 2) Gravar. POST COMUM: sem `AJAXREQUEST` e com o ViewState de DENTRO do form do modal —
        // mandar o do form0 faria o JSF restaurar a view errada e a ação não rodaria (200 mudo).
        var resposta = await sessao.SubmeterEscritaAsync(
            htmlModal,
            modal.FormId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [modal.CampoTexto] = texto.Trim(),
                [modal.BotaoGravar] = "Gravar",
                ["autoScroll"] = string.Empty,
            },
            modal.ViewState,
            $"registrar FollowUP na solicitação {idSer}",
            cancellationToken);

        // A resposta é a tela de pesquisa inteira de novo — reabsorver evita que a próxima
        // chamada parta de uma base velha.
        Absorver(resposta.Texto);

        return SerHtmlParser.MensagemDaTela(SerHtmlParser.Documento(resposta.Texto));
    }

    private void Absorver(string html)
    {
        _htmlDados = html;

        // ATENÇÃO: a tela de HISTÓRICO também tem um <form id="form0"> (as abas
        // Pesquisar/Editar/Historico vivem no mesmo form). Promover qualquer página com form0 a
        // "página do formulário" fazia a tela de histórico virar a base dos submits seguintes —
        // e ela não tem o botão Pesquisar. Resultado: o 1º histórico era lido e TODOS os
        // seguintes falhavam com "Botão Pesquisar não encontrado" (10.501 falhas na carga
        // inicial de 06/08/2026). Só é página de formulário quem tem o botão de pesquisa.
        var ehPaginaCompleta = html.Contains("<form id=\"form0\"", StringComparison.Ordinal);
        var ehPaginaDeFormulario =
            ehPaginaCompleta && SerHtmlParser.BotaoPesquisar(SerHtmlParser.Documento(html)) is not null;

        if (ehPaginaDeFormulario) _htmlForm = html;

        // O VIEWSTATE TEM DE SER DA MESMA VIEW QUE O FORM — senão o JSF restaura a view errada e
        // a ação não roda: HTTP 200, grade vazia, nenhum erro (docs/ser.md §3.2).
        //
        // Aceita de dois lugares: da página de formulário (mesma view do `_htmlForm`) e de
        // resposta PARCIAL — o datascroller re-renderiza a mesma view e devolve o ViewState novo,
        // que é o correto para o submit seguinte.
        //
        // RECUSA de outra página completa. A tela de HISTÓRICO é outra view: absorver o ViewState
        // dela deixava `_htmlForm` (pesquisa) e `_ultimoViewState` (histórico) apontando para
        // views diferentes, e a busca seguinte voltava vazia. Custou 14.363 falhas na carga
        // inicial de 08/08/2026 — o 1º histórico era lido e todos os outros morriam com
        // "devolveu 0 linhas". Medido contra o SER: mesma busca dá 1 linha com o ViewState da
        // pesquisa e 0 com o do histórico.
        if (!ehPaginaCompleta || ehPaginaDeFormulario)
        {
            var vs = SerHtmlParser.ViewStateQualquer(html);
            if (!string.IsNullOrEmpty(vs)) _ultimoViewState = vs;
        }
    }

    private static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
