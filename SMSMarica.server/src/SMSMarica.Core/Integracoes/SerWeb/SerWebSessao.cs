using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Credenciais;

namespace SMSMarica.Core.Integracoes.SerWeb;

/// <summary>
/// Sessão autenticada (web scraping) do SER — Sistema Estadual de Regulação da SES-RJ.
/// Protocolo completo em <c>docs/ser.md</c>; decisão em ADR-0042.
///
/// <para><b>SOMENTE LEITURA.</b> Todo POST passa por <see cref="GarantirLeitura"/>. Login e
/// formulários de PESQUISA são permitidos porque não alteram dado; qualquer componente cujo nome
/// ou rótulo visível sugira escrita é recusado antes de sair da máquina.</para>
///
/// <para><b>Sessão única:</b> como no SISREG, um login novo derruba a sessão anterior daquele
/// operador — inclusive a do humano. Mantemos um cliente com cookies próprios e serializamos as
/// chamadas por semáforo, para que duas varreduras não se derrubem.</para>
/// </summary>
public interface ISerWebSessao
{
    /// <summary>Garante sessão logada + módulo ativo e devolve a tela de pesquisa (HTML completo).</summary>
    Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken);

    /// <summary>Garante sessão logada + módulo ativo e devolve QUALQUER tela do módulo. A tela de
    /// Histórico de Consulta/Exame (a do export) mora em outro caminho, mas na mesma conversa Seam.</summary>
    Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken);

    /// <summary>Submete o form da tela de pesquisa. <paramref name="htmlForm"/> é o último HTML
    /// COMPLETO (com <c>&lt;form id="form0"&gt;</c>); a resposta de paginação é parcial.</summary>
    Task<string> SubmeterPesquisaAsync(
        string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
        CancellationToken cancellationToken);

    /// <summary>
    /// Submete um form qualquer e devolve a resposta <b>em bytes</b>.
    ///
    /// <para>O botão <i>Exportar</i> responde um <c>.xls</c> BIFF8 (OLE2), não HTML: decodificar a
    /// resposta como UTF-8 corrompe a planilha de forma irreversível. Por isso o transporte devolve
    /// bytes e quem sabe o que pediu decide se aquilo é texto ou arquivo.</para>
    /// </summary>
    Task<RespostaSer> SubmeterFormAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, CancellationToken cancellationToken);

    /// <summary>
    /// Submete um form <b>ESCREVENDO</b> no SER: a trava de somente-leitura não roda aqui.
    ///
    /// <para>Existe como método separado, e não como um <c>bool</c> em
    /// <see cref="SubmeterFormAsync"/>, para que toda escrita seja uma decisão visível na chamada
    /// — dá para achar todas com um grep pelo nome. O <paramref name="operacao"/> é obrigatório e
    /// vai para o log: quando o Estado perguntar "quem fez isso", a resposta tem de estar aqui.</para>
    ///
    /// <para><b>Só use depois de autorização explícita para AQUELA operação.</b> Hoje existe uma:
    /// registrar FollowUP (docs/ser.md §9). Qualquer outra precisa do mesmo caminho — autorizar,
    /// mapear contra o SER real e só então chamar isto.</para>
    /// </summary>
    Task<RespostaSer> SubmeterEscritaAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, string operacao, CancellationToken cancellationToken);

    /// <summary>Autentica uma credencial avulsa (ainda não salva) — usado na tela de configuração.</summary>
    Task<string> AutenticarAvulsoAsync(string usuario, string senha, CancellationToken cancellationToken);

    /// <summary>
    /// Amarra ESTA instância à credencial de um operador, em vez da credencial de sincronismo
    /// guardada no banco.
    ///
    /// <para><b>Por que existe:</b> a trilha de auditoria do SER grava o nome de quem fez, e a
    /// credencial do banco é de SINCRONISMO — usá-la para escrever faria toda ação do município
    /// sair no nome da mesma pessoa, apagando a autoria real. Escrita usa a credencial de quem
    /// está operando (docs/ser.md §9).</para>
    ///
    /// <para>A senha fica só na memória desta instância; nada disso é persistido.</para>
    /// </summary>
    void UsarCredencialDoOperador(string usuario, string senha);

    /// <summary>Descarta a sessão em memória (força novo login na próxima chamada).</summary>
    void Reiniciar();
}

/// <summary>A trava de somente-leitura recusou a operação. É <b>bug do motor</b>, não do SER.</summary>
public sealed class EscritaNoSerBloqueadaException(string mensagem) : Exception(mensagem);

/// <summary>A linha não oferece "Histórico da Solicitação" (acontece na situação Alta).</summary>
public sealed class HistoricoSerIndisponivelException(string mensagem) : Exception(mensagem);

public sealed partial class SerWebSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<SerWebSessao> logger) : ISerWebSessao, IDisposable
{
    public const string Provedor = "ser";
    private const string BaseUrlPadrao = "https://ser.saude.rj.gov.br";
    private const string CaminhoLogin = "/ser/login";
    private const string CaminhoHome = "/ser/home.seam";

    public const string CaminhoPesquisa =
        "/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam";

    private const string ModuloPadrao = "ambulatorial";

    /// <summary>
    /// Verbos que denunciam ação de ESCRITA. Confere contra o nome do parâmetro <b>e</b> contra o
    /// rótulo visível do componente — uma camada só não cobre JSF: o botão <i>Gravar</i> do modal
    /// de cancelamento é <c>j_id189:j_id195</c> e <b>Registrar FollowUP</b> é <c>j_id169</c>; o
    /// verbo só existe no texto do elemento, nunca no nome.
    /// </summary>
    [GeneratedRegex(
        "(salvar|gravar|confirmar|inserir|incluir|alterar|editar|atualizar|excluir|remover|deletar|"
        + "apagar|cancelar|agendar|marcar|desmarcar|reservar|autorizar|negar|devolver|encaminhar|"
        + "executar|efetivar|finalizar|aprovar|reprovar|transferir|faltou|absenteismo|registrar|"
        + "followup|follow.?up|pendenciar|dar.?alta)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexEscrita();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Sessao? _sessao;

    /// <summary>Credencial do OPERADOR, quando esta instância é de escrita. Só memória.</summary>
    private Credenciais? _credencialDoOperador;

    public void UsarCredencialDoOperador(string usuario, string senha)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException(
                "ser.credencial_operador_incompleta",
                "Informe o usuário e a senha do SER.");
        }

        _credencialDoOperador = new Credenciais(usuario, senha, new Uri(BaseUrlPadrao));
        Reiniciar();
    }

    public void Reiniciar()
    {
        _sessao?.Dispose();
        _sessao = null;
    }

    // ------------------------------------------------------------------ público

    public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) =>
        AbrirTelaAsync(CaminhoPesquisa, cancellationToken);

    public async Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var html = await GetAsync(sessao, caminho, cancellationToken);

            // HTTP 500 aqui significa quase sempre "módulo não ativo na sessão" (o Seam exige a
            // navegação pelo menu). Refazemos o caminho uma vez antes de desistir.
            if (html is null)
            {
                logger.LogInformation("SER: tela {Caminho} recusada — reativando o módulo.", caminho);
                await EntrarNoModuloAsync(sessao, cancellationToken);
                html = await GetAsync(sessao, caminho, cancellationToken);
            }

            // SESSÃO MORTA NÃO DÁ 401 — dá HTTP 200 com a tela de login. Como `Logado` só era
            // marcado no login e nunca desmarcado, `GarantirSessaoAsync` considerava a sessão boa
            // para sempre: o GET voltava a tela de login, quem chamou não achava o botão
            // Pesquisar e a varredura inteira morria em menos de um segundo — sem tentar
            // reautenticar, e só tentando de novo no dia seguinte.
            //
            // Aconteceu em 09/08 10:55 e 10/08 05:30 (a diária). Sessão do SER é única por
            // operador: qualquer outro login com a mesma credencial — inclusive as sondas do
            // laboratório — derruba a do servidor. E mesmo sem ninguém derrubar, sessão expira.
            if (html is not null && EhTelaDeLogin(html))
            {
                logger.LogWarning(
                    "SER: a sessão tinha morrido (o SER devolveu a tela de login em {Caminho}). "
                    + "Reautenticando e tentando de novo.", caminho);

                Reiniciar();
                sessao = await GarantirSessaoAsync(cancellationToken);
                html = await GetAsync(sessao, caminho, cancellationToken);

                if (html is not null && EhTelaDeLogin(html))
                {
                    throw new ValidacaoException(
                        "ser.sessao_nao_recuperada",
                        "O SER devolveu a tela de login mesmo depois de reautenticar. A credencial "
                        + "pode ter sido bloqueada, ou o SER está recusando novas sessões.");
                }
            }

            return html ?? throw new ValidacaoException(
                "ser.tela_indisponivel",
                $"O SER recusou a tela {caminho}. Verifique se a credencial tem acesso ao módulo "
                + "Ambulatório.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SubmeterPesquisaAsync(
        string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
        CancellationToken cancellationToken)
    {
        var resposta = await SubmeterFormAsync(
            htmlForm, SerHtmlParser.FormPesquisa, extras, viewState, cancellationToken);
        return resposta.Texto;
    }

    public Task<RespostaSer> SubmeterFormAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, CancellationToken cancellationToken)
    {
        GarantirLeitura(extras, SerHtmlParser.Documento(htmlPagina));
        return SubmeterAsync(htmlPagina, formId, extras, viewState, comoNavegador: false, cancellationToken);
    }

    public Task<RespostaSer> SubmeterEscritaAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, string operacao, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operacao))
        {
            throw new ArgumentException(
                "Toda escrita no SER precisa dizer QUAL operação é — o nome vai para o log.",
                nameof(operacao));
        }

        // Warning, não Debug: escrita no sistema do Estado é evento raro e precisa saltar do log.
        logger.LogWarning(
            "SER: ESCRITA — {Operacao} (form {Form}, {Campos} parâmetro(s)).",
            operacao, formId, extras.Count);

        // `comoNavegador`: escrita não manda campo travado de volta (identidade do paciente).
        return SubmeterAsync(htmlPagina, formId, extras, viewState, comoNavegador: true, cancellationToken);
    }

    private async Task<RespostaSer> SubmeterAsync(
        string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
        string? viewState, bool comoNavegador, CancellationToken cancellationToken)
    {
        var doc = SerHtmlParser.Documento(htmlPagina);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);

            var campos = SerHtmlParser.CamposDoForm(doc, formId, comoNavegador);
            campos[formId] = formId;
            foreach (var (k, v) in extras) campos[k] = v;

            // ViewState: o fresco vence. A resposta de paginação é parcial (traz a grade sem
            // form nenhum), então quem pagina passa o ViewState lido da última resposta.
            var vs = viewState
                     ?? SerHtmlParser.ViewStateDoForm(doc, formId)
                     ?? sessao.UltimoViewState;
            if (!string.IsNullOrEmpty(vs)) campos["javax.faces.ViewState"] = vs;

            // `AJAX:EVENTS_COUNT` só faz sentido em submit A4J. O Exportar é um commandLink comum
            // (`jsfcljs`, Mojarra) e não é ajax — mandar contador de evento ajax nele é ruído.
            if (campos.ContainsKey("AJAXREQUEST")) campos["AJAX:EVENTS_COUNT"] = "1";

            // POSTAR NO `action` DO FORM, NUNCA NUMA CONSTANTE. Descoberto em 06/08/2026: com os
            // MESMOS campos, headers e ViewState, postar no caminho fixo devolve um conjunto de
            // resultados DIFERENTE do que a tela mostra — registros que existem (e são
            // encontráveis por ID) somem da listagem. Só postando no action lido da página o
            // resultado bate com o do navegador. É a mesma regra que o cliente do SISREG já
            // documenta ("o action vem com ;jsessionid — usar cru").
            var destino = SerHtmlParser.ActionDoForm(doc, formId)
                          ?? throw new ValidacaoException(
                              "ser.form_sem_action",
                              $"O form '{formId}' da tela do SER veio sem `action`. Postar em caminho "
                              + "constante devolve listagem incompleta — ver docs/ser.md §3.3.");

            var resposta = await PostAsync(sessao, destino, campos, cancellationToken);

            // Sessão morta no meio de um POST: aqui NÃO se repete a requisição. O ViewState era
            // da sessão antiga e a nova view é outra — repostar cairia na armadilha de sempre
            // (HTTP 200, view errada restaurada, resultado silenciosamente incompleto:
            // docs/ser.md §3.3). Só marcamos a sessão como morta, para a próxima chamada
            // reautenticar em vez de insistir num cadáver, e devolvemos erro nomeado.
            if (resposta.EhTexto && EhTelaDeLogin(resposta.Texto))
            {
                logger.LogWarning("SER: a sessão morreu durante um POST em {Destino}.", destino);
                Reiniciar();
                throw new ValidacaoException(
                    "ser.sessao_expirada",
                    "A sessão do SER caiu no meio da operação. A próxima tentativa reautentica "
                    + "sozinha — refaça a rodada.");
            }

            if (resposta.EhTexto) AbsorverViewState(sessao, resposta.Texto);
            return resposta;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> AutenticarAvulsoAsync(
        string usuario, string senha, CancellationToken cancellationToken)
    {
        // Cliente descartável: credencial errada não contamina o cookie jar da sessão de trabalho.
        // Ainda assim derruba a sessão humana daquele operador (sessão única no SER).
        var sessao = new Sessao(new Uri(BaseUrlPadrao));
        try
        {
            await LoginAsync(sessao, usuario, senha, cancellationToken);
            await EntrarNoModuloAsync(sessao, cancellationToken);
            var html = await GetAsync(sessao, CaminhoPesquisa, cancellationToken);
            return html ?? throw new ValidacaoException(
                "ser.sem_acesso_ambulatorio",
                "O SER autenticou, mas a credencial não abriu o módulo Ambulatório.");
        }
        finally
        {
            sessao.Dispose();
        }
    }

    // ------------------------------------------------------------------ trava

    /// <summary>
    /// Recusa POSTs que pareçam escrita. Duas camadas: nome do parâmetro (pega ids falantes) e
    /// rótulo visível do componente clicado (indispensável — o SER usa ids opacos).
    /// </summary>
    /// <summary>
    /// Parâmetros de NAVEGAÇÃO liberados nominalmente, apesar de casarem com o regex de escrita.
    ///
    /// <para><c>form0:editar_server_submit</c> só TROCA DE ABA (é o <c>_JSFFormSubmit</c> do
    /// <c>rich:tab</c>): abre o formulário de criação para leitura, sem gravar coisa alguma —
    /// quem grava é o botão <i>Gravar</i>, que continua barrado pelas duas camadas. O verbo está
    /// no NOME da aba, não na ação.</para>
    ///
    /// <para>É uma lista fechada e nominal de propósito: afrouxar o regex abriria a porta para
    /// <c>btnEditar</c> de verdade em qualquer tela.</para>
    /// </summary>
    private static readonly HashSet<string> NavegacaoLiberada = new(StringComparer.Ordinal)
    {
        "form0:editar_server_submit",
        "form0:pesquisar_server_submit",
    };

    internal static void GarantirLeitura(IReadOnlyDictionary<string, string> extras, IHtmlDocument doc)
    {
        foreach (var chave in extras.Keys)
        {
            if (NavegacaoLiberada.Contains(chave)) continue;

            if (RegexEscrita().IsMatch(chave))
            {
                throw new EscritaNoSerBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o parâmetro '{chave}' parece "
                    + "acionar uma ação de escrita no SER.");
            }

            var el = doc.GetElementById(chave)
                     ?? doc.QuerySelector($"[name=\"{CssEscape(chave)}\"]");
            if (el is null) continue;

            var rotulo = string.Join(' ', new[]
            {
                el.GetAttribute("value"),
                el.GetAttribute("title"),
                el.TextContent,
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            if (RegexEscrita().IsMatch(rotulo))
            {
                var curto = rotulo.Trim();
                if (curto.Length > 60) curto = curto[..60];
                throw new EscritaNoSerBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o componente '{chave}' tem "
                    + $"rótulo '{curto}' — é ação de escrita no SER.");
            }
        }
    }

    private static string CssEscape(string valor) => valor.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// A resposta é a tela de login? É assim — e só assim — que o SER avisa que a sessão morreu:
    /// HTTP 200 com o formulário de login no corpo, nunca 401 nem redirect.
    ///
    /// <para>Mesma checagem que <c>LoginAsync</c> usa para detectar credencial recusada. Os dois
    /// casos têm a mesma cara na resposta; o que os separa é o momento — no login é senha errada,
    /// no meio da navegação é sessão perdida.</para>
    /// </summary>
    internal static bool EhTelaDeLogin(string html) =>
        html.Contains("id=\"login:username\"", StringComparison.Ordinal)
        || html.Contains("name=\"login:password\"", StringComparison.Ordinal);

    // ------------------------------------------------------------------ navegação

    private async Task<Sessao> GarantirSessaoAsync(CancellationToken cancellationToken)
    {
        if (_sessao is { Logado: true, ModuloAtivo: true }) return _sessao;

        var creds = await CarregarCredenciaisAsync(cancellationToken);
        _sessao?.Dispose();
        _sessao = new Sessao(creds.BaseUri);

        await LoginAsync(_sessao, creds.Usuario, creds.Senha, cancellationToken);
        await EntrarNoModuloAsync(_sessao, cancellationToken);
        return _sessao;
    }

    private async Task LoginAsync(Sessao sessao, string usuario, string senha, CancellationToken cancellationToken)
    {
        sessao.Logado = false;
        sessao.ModuloAtivo = false;

        var pagina = await GetAsync(sessao, CaminhoLogin, cancellationToken)
            ?? throw new ValidacaoException("ser.login_indisponivel", "A tela de login do SER não respondeu.");

        var doc = SerHtmlParser.Documento(pagina);
        // O action do form já vem com ";jsessionid=..." — usar cru, não reescrever. E SEM
        // fallback para caminho constante: postar em constante devolve resultado diferente sem
        // erro (docs/ser.md §3.3), e o fallback aqui faria a falha aflorar depois com o
        // diagnóstico ERRADO ("verifique usuário e senha").
        var action = SerHtmlParser.ActionDoForm(doc, "login")
            ?? throw new ValidacaoException(
                "ser.form_sem_action",
                "O form de login do SER veio sem `action` — layout mudou? Postar em caminho "
                + "constante devolve comportamento diferente sem erro (docs/ser.md §3.3).");

        var campos = SerHtmlParser.CamposDoForm(doc, "login");
        campos["login"] = "login";
        campos["login:username"] = usuario;
        campos["login:password"] = senha;
        campos["login:entrar"] = "Entrar";
        campos["javax.faces.ViewState"] = SerHtmlParser.ViewStateDoForm(doc, "login") ?? "j_id1";

        var html = (await PostAsync(sessao, action, campos, cancellationToken)).Texto;

        // A tela de login de volta = credencial recusada.
        if (html.Contains("id=\"login:username\"", StringComparison.Ordinal)
            || html.Contains("name=\"login:password\"", StringComparison.Ordinal))
        {
            throw new ValidacaoException(
                "ser.login_falhou",
                "Não foi possível autenticar no SER. Verifique o usuário e a senha cadastrados "
                + "na Configuração da Regulação.");
        }

        sessao.Logado = true;
        AbsorverViewState(sessao, html);
    }

    /// <summary>
    /// Ativa o módulo Ambulatório na sessão Seam. <b>O parâmetro <c>AJAXREQUEST</c> é
    /// obrigatório</b>: sem ele o WildFly trata o POST como postback comum, re-renderiza a home e
    /// a ação nem roda — HTTP 200, sem erro nenhum (docs/ser.md §3.1).
    /// </summary>
    private async Task EntrarNoModuloAsync(Sessao sessao, CancellationToken cancellationToken)
    {
        var home = await GetAsync(sessao, CaminhoHome, cancellationToken)
            ?? throw new ValidacaoException("ser.home_indisponivel", "A home do SER não respondeu.");

        var formId = SerHtmlParser.FormDeModulo(home)
            ?? throw new ValidacaoException(
                "ser.home_sem_modulos",
                "Não foi possível localizar a escolha de módulo na home do SER (layout mudou?).");

        var doc = SerHtmlParser.Documento(home);
        var campos = SerHtmlParser.CamposDoForm(doc, formId);
        campos[formId] = formId;
        campos[$"{formId}:goModulo"] = $"{formId}:goModulo";
        campos["param1"] = ModuloPadrao;
        campos["AJAXREQUEST"] = formId;
        campos["AJAX:EVENTS_COUNT"] = "1";
        var vs = SerHtmlParser.ViewStateDoForm(doc, formId) ?? SerHtmlParser.ViewStateQualquer(home);
        if (!string.IsNullOrEmpty(vs)) campos["javax.faces.ViewState"] = vs;

        // Mesma regra do submit de pesquisa: o destino sai do `action` da página, não de
        // constante — e sem fallback, senão a falha aflora como "ser.modulo_nao_ativou" com o
        // diagnóstico errado (docs/ser.md §3.3).
        var acaoModulo = SerHtmlParser.ActionDoForm(doc, formId)
            ?? throw new ValidacaoException(
                "ser.form_sem_action",
                $"O form '{formId}' da home do SER veio sem `action` — layout mudou?");
        var resposta = await PostAsync(sessao, acaoModulo, campos, cancellationToken);

        // A resposta é um redirect A4J: header Location (aqui) ou <meta> no corpo (histórico).
        var destino = resposta.Location ?? SerHtmlParser.RedirectNoCorpo(resposta.Texto);
        if (string.IsNullOrWhiteSpace(destino))
        {
            throw new ValidacaoException(
                "ser.modulo_nao_ativou",
                $"O SER não redirecionou após escolher o módulo '{ModuloPadrao}'. Isso costuma "
                + "significar que o parâmetro AJAXREQUEST deixou de ser aceito — ver docs/ser.md.");
        }

        // Seguir o Location é o que de fato ativa o módulo na conversa Seam.
        await GetAsync(sessao, destino, cancellationToken);
        sessao.ModuloAtivo = true;
    }

    /// <summary>Segue o redirect A4J embutido no corpo e devolve a página de destino.</summary>
    public async Task<string?> SeguirRedirectNoCorpoAsync(string corpo, CancellationToken cancellationToken)
    {
        var destino = SerHtmlParser.RedirectNoCorpo(corpo);
        if (string.IsNullOrWhiteSpace(destino)) return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessao = await GarantirSessaoAsync(cancellationToken);
            var html = await GetAsync(sessao, destino, cancellationToken);
            if (html is not null) AbsorverViewState(sessao, html);
            return html;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ------------------------------------------------------------------ HTTP

    /// <summary>GET autenticado. Devolve <c>null</c> em 5xx — o chamador decide se reativa o módulo.</summary>
    private static async Task<string?> GetAsync(Sessao sessao, string caminho, CancellationToken cancellationToken)
    {
        var uri = new Uri(sessao.BaseUri, caminho);
        using var resposta = await sessao.Http.GetAsync(uri, cancellationToken);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);
        return (int)resposta.StatusCode >= 500 ? null : Encoding.UTF8.GetString(bytes);
    }

    private static async Task<RespostaSer> PostAsync(
        Sessao sessao, string caminho, IReadOnlyDictionary<string, string> campos,
        CancellationToken cancellationToken)
    {
        using var conteudo = new FormUrlEncodedContent(campos);
        var uri = new Uri(sessao.BaseUri, caminho);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, uri) { Content = conteudo };
        requisicao.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        using var resposta = await sessao.Http.SendAsync(requisicao, cancellationToken);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(cancellationToken);

        // O A4J devolve 200 + header Location (não é 3xx, então o HttpClient não segue sozinho).
        var location = resposta.Headers.Location?.ToString()
                       ?? (resposta.Headers.TryGetValues("location", out var vs) ? vs.FirstOrDefault() : null);

        return new RespostaSer(
            bytes,
            resposta.Content.Headers.ContentType?.MediaType,
            resposta.Content.Headers.ContentDisposition?.FileName?.Trim('"'),
            location);
    }

    private static void AbsorverViewState(Sessao sessao, string html)
    {
        var vs = SerHtmlParser.ViewStateQualquer(html);
        if (!string.IsNullOrEmpty(vs)) sessao.UltimoViewState = vs;
    }

    // ------------------------------------------------------------------ credencial

    private sealed record Credenciais(string Usuario, string Senha, Uri BaseUri);

    private async Task<Credenciais> CarregarCredenciaisAsync(CancellationToken cancellationToken)
    {
        // Sessão do OPERADOR: o par usuário/senha já veio dele; do provedor só aproveitamos a URL,
        // e a ausência do provedor NÃO pode derrubar o login — a credencial dele está correta, e
        // "Provedor 'ser' ainda não configurado" seria um diagnóstico mentiroso na cara de quem
        // acabou de digitar a senha certa.
        if (_credencialDoOperador is { } doOperador)
        {
            return doOperador with { BaseUri = new Uri(await BaseUrlConfiguradaAsync(cancellationToken)) };
        }

        using var scope = scopeFactory.CreateScope();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "ser.credencial_incompleta",
                "Configure o usuário e a senha do SER em Regulação → Configuração → SER.");
        }

        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, new Uri(LerBaseUrl(ctx.ParametrosJson)));
    }

    /// <summary>URL do SER configurada, caindo no padrão quando o provedor não existe/está
    /// inativo — usado só pela sessão do operador, que não depende do provedor para nada mais.</summary>
    private async Task<string> BaseUrlConfiguradaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
            var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);
            return LerBaseUrl(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            return BaseUrlPadrao;
        }
    }

    private static string LerBaseUrl(string? parametrosJson)
    {
        if (string.IsNullOrWhiteSpace(parametrosJson)) return BaseUrlPadrao;
        try
        {
            using var doc = JsonDocument.Parse(parametrosJson);
            if (doc.RootElement.TryGetProperty("baseUrl", out var b)
                && b.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(b.GetString()))
            {
                return b.GetString()!;
            }
        }
        catch (JsonException)
        {
            // parametrosJson inválido → default.
        }
        return BaseUrlPadrao;
    }

    /// <summary>Uma sessão do SER: cookie jar próprio (JSESSIONID + SERVERID do balanceador).</summary>
    private sealed class Sessao(Uri baseUri) : IDisposable
    {
        public HttpClient Http { get; } = CriarHttp();
        public Uri BaseUri { get; } = baseUri;
        public bool Logado { get; set; }
        public bool ModuloAtivo { get; set; }

        /// <summary>ViewState mais recente — necessário porque a resposta de paginação é parcial
        /// e não traz form nenhum de onde extraí-lo.</summary>
        public string? UltimoViewState { get; set; }

        private static HttpClient CriarHttp()
        {
            var handler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                AllowAutoRedirect = true,
            };
            var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                + "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en;q=0.8");
            return http;
        }

        public void Dispose() => Http.Dispose();
    }

    public void Dispose()
    {
        _sessao?.Dispose();
        _gate.Dispose();
    }
}
