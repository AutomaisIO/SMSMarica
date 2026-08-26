using System.Globalization;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Logging;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura;

/// <summary>
/// Leitura da fila do SERNIT: pesquisa, paginação e histórico. Camada que fala "SERNIT" — não
/// conhece o banco. Espelho do <c>SerLeitorService</c> do SER-RJ; adaptações medidas no lab:
/// <list type="bullet">
/// <item>Situação resolvida pelo <c>&lt;select&gt;</c> que oferece <c>EM_FILA</c> (id volátil).</item>
/// <item>Itens do menu da linha (Histórico/Editar/FollowUP) abrem por A4J <c>_viewRoot</c>+<c>ajaxSingle</c>.</item>
/// <item>A grade traz <c>TotalReal</c>/<c>Capada</c> (via <c>form0:msgErro</c>) — insumo do varredor.</item>
/// </list>
///
/// <para><b>Estado interno:</b> guarda o último HTML COMPLETO (fonte dos campos) e o último recebido
/// (fonte das linhas), pela mesma razão do SER-RJ — a resposta do datascroller é parcial.</para>
/// </summary>
public interface ISernitLeitorService
{
    Task PrepararAsync(CancellationToken cancellationToken);

    Task<SernitPaginaGrade> PesquisarAsync(SernitFiltroPesquisa filtro, CancellationToken cancellationToken);

    Task<IReadOnlyList<SernitLinhaGrade>> IrParaPaginaAsync(int pagina, CancellationToken cancellationToken);

    Task<SernitHistorico> AbrirHistoricoAsync(int indiceNaPagina, CancellationToken cancellationToken);

    Task<SernitHistorico> LerHistoricoPorIdAsync(
        string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken);

    /// <summary>Registra um FollowUP (observação). <b>ESCREVE no SERNIT.</b> A mensagem devolvida
    /// não é prova — quem confirma é a releitura do histórico.</summary>
    Task<string> RegistrarFollowUpAsync(
        string idSernit, SituacaoSernit situacao, string texto, CancellationToken cancellationToken);

    /// <summary>Abre a aba Editar e lê os telefones, sem gravar.</summary>
    Task<SernitContatosDaTela> LerContatosAsync(
        string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken);

    /// <summary>Altera os telefones na aba Editar. <b>ESCREVE no SERNIT.</b> Trava invertida: só os
    /// campos-alvo podem divergir do que a tela renderizou. Devolve os contatos RELIDOS.
    /// <b>Exige CPF no cadastro</b> — o SERNIT recusa o Gravar com "CPF é obrigatório" se faltar.</summary>
    Task<SernitContatosDaTela> AlterarContatosAsync(
        string idSernit, SituacaoSernit situacao, IReadOnlyDictionary<string, string> novos,
        CancellationToken cancellationToken);
}

/// <summary>A linha não oferece "Editar" (situações terminais, como Cancelada e Alta).</summary>
public sealed class EdicaoSernitIndisponivelException(string mensagem) : Exception(mensagem);

/// <summary>A linha não oferece "Registrar FollowUP" no menu Opções (situação Alta).</summary>
public sealed class FollowUpSernitIndisponivelException(string mensagem) : Exception(mensagem);

public sealed class SernitLeitorService(
    ISernitWebSessao sessao,
    ILogger<SernitLeitorService> logger) : ISernitLeitorService
{
    private const string CampoTipo = "form0:comboTipoRecurso";
    private const string CampoCpf = "form0:cpf";
    private const string CampoNome = "form0:nome";
    private const string CampoCns = "form0:cns";
    private const string CampoIdSolicitacao = "form0:idSolicitacao";
    private const string CampoDataInicio = "form0:dtInicialSolicitacaoInputDate";
    private const string CampoDataFim = "form0:dtFinalSolicitacaoInputDate";
    private const string SituacaoFallback = "form0:j_id54";
    private const string RegiaoViewRoot = "_viewRoot";

    private string _htmlForm = string.Empty;
    private string _htmlDados = string.Empty;
    private string? _ultimoViewState;

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        var html = await sessao.AbrirTelaPesquisaAsync(cancellationToken);
        Absorver(html);
    }

    public async Task<SernitPaginaGrade> PesquisarAsync(
        SernitFiltroPesquisa filtro, CancellationToken cancellationToken)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            if (string.IsNullOrEmpty(_htmlForm)) await PrepararAsync(cancellationToken);

            var doc = SernitHtmlParser.Documento(_htmlForm);
            if (SernitHtmlParser.BotaoPesquisar(doc) is null)
            {
                // A sessão do motor caiu no SERNIT — a varredura DIÁRIA reusa o singleton, que ficou
                // parado desde a última rodada; o SERNIT devolve uma página de sessão-expirada que NÃO
                // bate com o detector de login/AGUARDE, então o AbrirTela não reautentica sozinho (a
                // CARGA INICIAL funciona justamente porque abre sessão nova). Força sessão nova e reabre.
                logger.LogInformation(
                    "SERNIT: tela de pesquisa perdida (sessão caída?) — reautenticando e reabrindo.");
                sessao.Reiniciar();
                _htmlForm = string.Empty;
                _htmlDados = string.Empty;
                _ultimoViewState = null;
                await PrepararAsync(cancellationToken);
                doc = SernitHtmlParser.Documento(_htmlForm);
            }

            var botao = SernitHtmlParser.BotaoPesquisar(doc)
                ?? throw new InvalidOperationException(
                    "Botão Pesquisar não encontrado na tela do SERNIT nem após reabri-la.");

            // Situação é OBRIGATÓRIA — resolvida pelo select que oferece EM_FILA (id volátil).
            var campoSituacao = SernitHtmlParser.SelectComOpcao(doc, SernitHtmlParser.FormPesquisa, "EM_FILA")
                                ?? SituacaoFallback;

            var extras = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [botao] = botao,
                ["AJAXREQUEST"] = SernitHtmlParser.FormPesquisa,
                [campoSituacao] = SernitCodigos.Codigo(filtro.Situacao),
            };

            if (filtro.Tipo is { } tipo) extras[CampoTipo] = SernitCodigos.Codigo(tipo);

            // SEMPRE seta os campos opcionais (valor OU vazio). É a correção da regressão da carga
            // inicial: a resposta do GRADE vem como página completa com as DATAS do fatiamento
            // preenchidas, e o Absorver a guarda como _htmlForm. Sem limpar, a busca por ID seguinte
            // HERDA essas datas e vira (situação, id, data-estreita) → 0 linhas (o id cai fora da
            // janela). Setando vazio, cada busca reflete só o filtro pedido. (Confirmado no lab.)
            extras[CampoDataInicio] = filtro.DataSolicitacaoInicio is { } di ? Br(di) : string.Empty;
            extras[CampoDataFim] = filtro.DataSolicitacaoFim is { } df ? Br(df) : string.Empty;
            extras[CampoCpf] = filtro.Cpf ?? string.Empty;
            extras[CampoNome] = filtro.Nome ?? string.Empty;
            extras[CampoCns] = filtro.Cns ?? string.Empty;
            extras[CampoIdSolicitacao] = filtro.IdSolicitacao ?? string.Empty;

            var html = await sessao.SubmeterPesquisaAsync(_htmlForm, extras, _ultimoViewState, cancellationToken);
            Absorver(html);

            var respostaDoc = SernitHtmlParser.Documento(_htmlDados);
            var pagina = new SernitPaginaGrade(
                SernitHtmlParser.LerGrade(respostaDoc),
                SernitHtmlParser.PaginasNaResposta(respostaDoc),
                SernitHtmlParser.TotalDeResultados(respostaDoc),
                SernitHtmlParser.GradeCapada(respostaDoc));

            // Busca por ID DEVE achar a linha (1). Se veio VAZIA, o form/viewstate herdado da fase
            // anterior (a grade termina no meio de um fatiamento por data) provavelmente está stale:
            // o SERNIT re-renderiza a view antiga e o filtro de ID não pega. Reabre o form LIMPO e
            // repete uma vez — é o que o lab faz (GET fresco por busca) e resolve. Sem ID, 0 é 0.
            if (pagina.Linhas.Count == 0 && !string.IsNullOrWhiteSpace(filtro.IdSolicitacao) && tentativa < 2)
            {
                // DIAG temporário: o que o SERNIT devolveu na busca por ID vazia? (login? grade? total?)
                var b = _htmlDados ?? string.Empty;
                logger.LogWarning(
                    "SERNIT/diag busca-id vazia: id={Id} sit={Sit} len={Len} login={Login} "
                    + "temListagem={Grid} temMsgErro={Msg} temIdSolic={IdF} totalTxt={Total}",
                    filtro.IdSolicitacao, filtro.Situacao, b.Length,
                    b.Contains("login:password", StringComparison.Ordinal),
                    b.Contains("form0:listagem", StringComparison.Ordinal),
                    b.Contains("form0:msgErro", StringComparison.Ordinal),
                    b.Contains("idSolicitacao", StringComparison.Ordinal),
                    (SernitHtmlParser.TotalDeResultados(respostaDoc)?.ToString() ?? "null"));

                logger.LogInformation(
                    "SERNIT: busca pelo ID {Id} veio vazia — reabrindo o formulário limpo e repetindo.",
                    filtro.IdSolicitacao);
                _htmlForm = string.Empty;
                _htmlDados = string.Empty;
                _ultimoViewState = null;
                continue;
            }

            return pagina;
        }
    }

    public async Task<IReadOnlyList<SernitLinhaGrade>> IrParaPaginaAsync(
        int pagina, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_htmlForm))
            throw new InvalidOperationException("Pesquise antes de paginar.");

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AJAXREQUEST"] = SernitHtmlParser.FormPesquisa,
            ["ajaxSingle"] = SernitHtmlParser.Scroller,
            [SernitHtmlParser.Scroller] = pagina.ToString(CultureInfo.InvariantCulture),
        };

        var html = await sessao.SubmeterPesquisaAsync(_htmlForm, extras, _ultimoViewState, cancellationToken);
        Absorver(html);
        return SernitHtmlParser.LerGrade(SernitHtmlParser.Documento(_htmlDados));
    }

    public async Task<SernitHistorico> AbrirHistoricoAsync(
        int indiceNaPagina, CancellationToken cancellationToken)
    {
        var dados = SernitHtmlParser.Documento(_htmlDados);
        var item = SernitHtmlParser.ItemHistorico(dados, indiceNaPagina);
        if (item is null)
        {
            var disponiveis = string.Join(", ", SernitHtmlParser.ItensDeOpcoes(dados, indiceNaPagina).Keys);
            throw new HistoricoSernitIndisponivelException(
                $"A linha {indiceNaPagina} não oferece 'Histórico da Solicitação' no menu Opções "
                + $"(solicitações em Alta não oferecem). Itens disponíveis: [{disponiveis}]");
        }

        // Item do menu da linha: A4J _viewRoot + ajaxSingle (medido no lab do SERNIT).
        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [item] = item,
            ["AJAXREQUEST"] = RegiaoViewRoot,
            ["ajaxSingle"] = item,
        };

        var resposta = await sessao.SubmeterPesquisaAsync(
            _htmlForm, extras, _ultimoViewState, cancellationToken);

        // O item responde 200 com um XHTML mínimo mandando redirecionar por <meta Location>.
        var destino = await sessao.SeguirRedirectNoCorpoAsync(resposta, cancellationToken);
        if (destino is not null) resposta = destino;

        Absorver(resposta);
        return SernitHtmlParser.LerHistorico(SernitHtmlParser.Documento(resposta));
    }

    public async Task<SernitHistorico> LerHistoricoPorIdAsync(
        string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken)
    {
        var pagina = await PesquisarAsync(
            new SernitFiltroPesquisa { Situacao = situacao, IdSolicitacao = idSernit }, cancellationToken);

        if (pagina.Linhas.Count != 1)
        {
            throw new InvalidOperationException(
                $"A busca pelo ID {idSernit} em {situacao} devolveu {pagina.Linhas.Count} linhas — "
                + "esperava exatamente 1. A solicitação pode ter mudado de situação.");
        }

        var historico = await AbrirHistoricoAsync(0, cancellationToken);

        var voltou = historico.IdSolicitacao;
        if (!string.IsNullOrWhiteSpace(voltou) && !string.Equals(voltou, idSernit, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O SERNIT devolveu o histórico da solicitação {voltou} quando pedimos {idSernit}. "
                + "Nada foi gravado.");
        }

        logger.LogDebug("SERNIT: histórico de {Id} lido com {Eventos} eventos.", idSernit, historico.Eventos.Count);
        return historico;
    }

    public async Task<string> RegistrarFollowUpAsync(
        string idSernit, SituacaoSernit situacao, string texto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ArgumentException("FollowUP sem texto não registra nada.", nameof(texto));
        }

        var pagina = await PesquisarAsync(
            new SernitFiltroPesquisa { Situacao = situacao, IdSolicitacao = idSernit }, cancellationToken);

        if (pagina.Linhas.Count != 1)
        {
            throw new InvalidOperationException(
                $"A busca pelo ID {idSernit} em {situacao} devolveu {pagina.Linhas.Count} linhas — "
                + "esperava exatamente 1. Nada foi escrito no SERNIT.");
        }

        var dados = SernitHtmlParser.Documento(_htmlDados);
        var item = SernitHtmlParser.ItemFollowUp(dados, 0);
        if (item is null)
        {
            var disponiveis = string.Join(", ", SernitHtmlParser.ItensDeOpcoes(dados, 0).Keys);
            throw new FollowUpSernitIndisponivelException(
                $"A solicitação {idSernit} não oferece 'Registrar FollowUP' no menu Opções. "
                + $"Itens disponíveis: [{disponiveis}]");
        }

        // 1) Abrir o modal (item cujo rótulo casa com a trava → porta de escrita).
        var abertura = await sessao.SubmeterEscritaAsync(
            _htmlForm,
            SernitHtmlParser.FormPesquisa,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [item] = item,
                ["AJAXREQUEST"] = RegiaoViewRoot,
                ["ajaxSingle"] = item,
            },
            _ultimoViewState,
            $"abrir modal de FollowUP da solicitação {idSernit}",
            cancellationToken);

        var htmlModal = abertura.Texto;
        var destino = await sessao.SeguirRedirectNoCorpoAsync(htmlModal, cancellationToken);
        if (destino is not null) htmlModal = destino;

        var modal = SernitHtmlParser.ModalDeObservacao(SernitHtmlParser.Documento(htmlModal))
            ?? throw new InvalidOperationException(
                "O SERNIT não devolveu o modal de FollowUP (form com textarea + botão Gravar). "
                + "O layout mudou? Nada foi escrito.");

        logger.LogInformation(
            "SERNIT: modal de FollowUP de {Id} — form {Form}, texto {Campo}, gravar {Botao}.",
            idSernit, modal.FormId, modal.CampoTexto, modal.BotaoGravar);

        // 2) Gravar. POST comum, com o ViewState de DENTRO do form do modal.
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
            $"registrar FollowUP na solicitação {idSernit}",
            cancellationToken);

        Absorver(resposta.Texto);

        var docResposta = SernitHtmlParser.Documento(resposta.Texto);
        var mensagem = SernitHtmlParser.MensagemDaTela(docResposta);

        if (string.IsNullOrWhiteSpace(mensagem))
        {
            logger.LogWarning(
                "SERNIT: gravação de FollowUP de {Id} voltou SEM mensagem de sucesso (HTML {Tamanho} B). "
                + "Diagnóstico estrutural — telaPesquisa(form0)={TemPesquisa}, caixaMensagens={TemCaixa}, "
                + "listagem={TemListagem}, modalAindaAberto={ModalAberto}.",
                idSernit,
                resposta.Texto.Length,
                docResposta.GetElementById(SernitHtmlParser.FormPesquisa) is not null,
                docResposta.GetElementById(SernitHtmlParser.CaixaMensagens) is not null,
                docResposta.GetElementById(SernitHtmlParser.TabelaGrade) is not null,
                SernitHtmlParser.ModalDeObservacao(docResposta) is not null);
        }

        return mensagem;
    }

    /// <summary>Rótulos dos telefones no SERNIT (medidos no lab): na aba Editar são "Telefone
    /// Residencial" e "Telefone Celular"; no Histórico, "Telefone SMS" é o de notificação. Cada
    /// chave aceita mais de um rótulo para sobreviver a uma padronização de texto da plataforma.</summary>
    private static readonly (string Chave, string[] Rotulos)[] RotulosDeTelefone =
    [
        ("residencial", ["Telefone Residencial"]),
        ("whatsapp", ["Telefone Celular", "Telefone SMS", "Telefone WhatsApp", "Telefone Whatsapp"]),
        ("contato", ["Telefone Contato", "Telefone de Contato", "Telefone"]),
    ];

    private static (string Nome, string Valor)? AcharTelefone(IHtmlDocument doc, string[] rotulos)
    {
        foreach (var rotulo in rotulos)
        {
            if (SernitHtmlParser.CampoPorRotulo(doc, SernitHtmlParser.FormPesquisa, rotulo) is { } achado)
            {
                return achado;
            }
        }
        return null;
    }

    public async Task<SernitContatosDaTela> LerContatosAsync(
        string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken)
    {
        var html = await AbrirEdicaoAsync(idSernit, situacao, cancellationToken);
        return LerContatos(SernitHtmlParser.Documento(html));
    }

    public async Task<SernitContatosDaTela> AlterarContatosAsync(
        string idSernit, SituacaoSernit situacao, IReadOnlyDictionary<string, string> novos,
        CancellationToken cancellationToken)
    {
        if (novos.Count == 0)
        {
            throw new ArgumentException("Nenhum telefone para alterar.", nameof(novos));
        }

        var html = await AbrirEdicaoAsync(idSernit, situacao, cancellationToken);
        var doc = SernitHtmlParser.Documento(html);

        var gravar = SernitHtmlParser.BotaoGravar(doc, SernitHtmlParser.FormPesquisa)
            ?? throw new InvalidOperationException(
                "Não achei o botão Gravar na aba Editar do SERNIT. Nada foi escrito.");

        var renderizado = SernitHtmlParser.CamposDoForm(doc, SernitHtmlParser.FormPesquisa, comoNavegador: true);
        var alterados = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (chave, rotulos) in RotulosDeTelefone)
        {
            if (!novos.TryGetValue(chave, out var valor)) continue;

            var campo = AcharTelefone(doc, rotulos)
                ?? throw new InvalidOperationException(
                    $"A aba Editar do SERNIT não trouxe o campo '{rotulos[0]}' (ou veio travado). "
                    + "Nada foi escrito.");

            if (!renderizado.ContainsKey(campo.Nome))
            {
                throw new InvalidOperationException(
                    $"O campo '{rotulos[0]}' ({campo.Nome}) não está entre os que a tela envia. "
                    + "Nada foi escrito.");
            }

            alterados[campo.Nome] = (valor ?? string.Empty).Trim();
        }

        if (alterados.Count == 0)
        {
            throw new ArgumentException("Nenhum telefone reconhecido para alterar.", nameof(novos));
        }

        // TRAVA INVERTIDA: só os campos-alvo podem divergir do que a tela renderizou.
        var foraDaTela = alterados.Keys.Where(k => !renderizado.ContainsKey(k)).ToList();
        if (foraDaTela.Count > 0)
        {
            throw new InvalidOperationException(
                $"TRAVA: o POST tocaria campos fora da tela ({string.Join(", ", foraDaTela)}). "
                + "Nada foi escrito.");
        }

        var extras = new Dictionary<string, string>(alterados, StringComparer.Ordinal)
        {
            [gravar] = gravar,
            ["AJAXREQUEST"] = SernitHtmlParser.RegiaoDoBotao(html, gravar) ?? SernitHtmlParser.FormPesquisa,
        };

        var resposta = await sessao.SubmeterEscritaAsync(
            html, SernitHtmlParser.FormPesquisa, extras,
            SernitHtmlParser.ViewStateDoForm(doc, SernitHtmlParser.FormPesquisa) ?? _ultimoViewState,
            $"alterar contatos da solicitação {idSernit} ({string.Join(", ", alterados.Keys)})",
            cancellationToken);

        var mensagem = SernitHtmlParser.MensagemDaTela(SernitHtmlParser.Documento(resposta.Texto));
        logger.LogInformation(
            "SERNIT: contatos de {Id} enviados ({Campos}); SERNIT respondeu: {Mensagem}",
            idSernit, string.Join(", ", alterados.Keys), mensagem);

        // CONFERÊNCIA: reabrir a edição do zero — "salvo com sucesso" não é prova.
        return await LerContatosAsync(idSernit, situacao, cancellationToken);
    }

    private async Task<string> AbrirEdicaoAsync(
        string idSernit, SituacaoSernit situacao, CancellationToken cancellationToken)
    {
        var pagina = await PesquisarAsync(
            new SernitFiltroPesquisa { Situacao = situacao, IdSolicitacao = idSernit }, cancellationToken);

        if (pagina.Linhas.Count != 1)
        {
            throw new InvalidOperationException(
                $"A busca pelo ID {idSernit} em {situacao} devolveu {pagina.Linhas.Count} linhas — "
                + "esperava exatamente 1. Nada foi escrito no SERNIT.");
        }

        var dados = SernitHtmlParser.Documento(_htmlDados);
        var item = SernitHtmlParser.ItemEditar(dados, 0);
        if (item is null)
        {
            var disponiveis = string.Join(", ", SernitHtmlParser.ItensDeOpcoes(dados, 0).Keys);
            throw new EdicaoSernitIndisponivelException(
                $"A solicitação {idSernit} não oferece 'Editar' no menu Opções — situações terminais "
                + $"não permitem alterar contato. Itens disponíveis: [{disponiveis}]");
        }

        // Abrir a edição é navegação, mas o rótulo "Editar" casa com a trava → porta de escrita.
        var resposta = await sessao.SubmeterEscritaAsync(
            _htmlForm,
            SernitHtmlParser.FormPesquisa,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [item] = item,
                ["AJAXREQUEST"] = RegiaoViewRoot,
                ["ajaxSingle"] = item,
            },
            _ultimoViewState,
            $"abrir edição da solicitação {idSernit}",
            cancellationToken);

        var html = resposta.Texto;
        if (SernitHtmlParser.RedirectNoCorpo(html) is { Length: > 0 } destinoEdit)
        {
            html = await sessao.AbrirTelaAsync(destinoEdit, cancellationToken);
        }

        Absorver(html);
        return html;
    }

    private static SernitContatosDaTela LerContatos(IHtmlDocument doc) => new(
        AcharTelefone(doc, RotulosDeTelefone[0].Rotulos),
        AcharTelefone(doc, RotulosDeTelefone[1].Rotulos),
        AcharTelefone(doc, RotulosDeTelefone[2].Rotulos));

    private void Absorver(string html)
    {
        _htmlDados = html;

        // A tela de HISTÓRICO/EDITAR também tem <form id="form0"> (as abas vivem no mesmo form).
        // Só é "página de formulário" quem tem o botão Pesquisar — senão a base dos submits
        // seguintes viraria a tela errada (o SER-RJ perdeu 10.501 históricos por isso).
        var ehPaginaCompleta = html.Contains("<form id=\"form0\"", StringComparison.Ordinal);
        var ehPaginaDeFormulario =
            ehPaginaCompleta && SernitHtmlParser.BotaoPesquisar(SernitHtmlParser.Documento(html)) is not null;

        if (ehPaginaDeFormulario) _htmlForm = html;

        // ViewState só da mesma view do form (página de formulário) ou de resposta parcial
        // (datascroller). Recusa o de outra página completa (histórico/editar são outra view).
        if (!ehPaginaCompleta || ehPaginaDeFormulario)
        {
            var vs = SernitHtmlParser.ViewStateQualquer(html);
            if (!string.IsNullOrEmpty(vs)) _ultimoViewState = vs;
        }
    }

    private static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
