using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Sessão autenticada (web scraping) do Klinikos (Eco Sistemas), "como usuário" — o caminho que
/// SUBSTITUI a leitura direta ao banco (ADITIVO: a estratégia SQL das UPAs continua). Espelha o
/// método de <c>SerWebSessao</c>, mas o alvo é ASP.NET WebForms + Telerik + Crystal, e há TRÊS
/// instâncias distintas (Conde, UPA, Santa Rita), cada uma um provedor de credencial próprio.
/// Protocolo medido em <c>Automais.klinikos/docs/APRENDIZADOS.md</c>.
///
/// <para><b>SOMENTE LEITURA.</b> O POST de leitura (paginação de tela) passa por
/// <see cref="GarantirLeitura"/>. Login e o gate do local de atendimento são POSTs de
/// configuração de sessão (não alteram dado clínico) e usam a porta interna liberada.</para>
///
/// <para><b>Sessão única por usuário:</b> um login novo derruba a sessão anterior do mesmo
/// usuário (inclusive a de uma pessoa) — por isso o conector usa usuário dedicado, mantém um
/// cliente com cookies próprios POR INSTÂNCIA e serializa por semáforo.</para>
/// </summary>
public interface IKlinikosWebSessao
{
    /// <summary>Baixa um relatório <c>rptviewXls.aspx</c> (BIFF) de uma instância. <paramref
    /// name="queryRelatorio"/> é o trecho após <c>/Relatorios/</c> (ex.:
    /// <c>rptviewXls.aspx?parRel=407&amp;parNum=4&amp;par1=0005&amp;...</c>). Relogina uma vez se a
    /// sessão tiver morrido.</summary>
    Task<byte[]> BaixarRelatorioXlsAsync(string provedor, string queryRelatorio, CancellationToken ct);

    /// <summary>GET autenticado de uma tela do app (HTML). Caminho relativo ao <c>AppRoot</c>.</summary>
    Task<string> AbrirTelaAsync(string provedor, string caminhoRelativo, CancellationToken ct);

    /// <summary>POST de LEITURA (paginação de tela): passa pela trava de somente-leitura.</summary>
    Task<string> SubmeterLeituraAsync(
        string provedor, string htmlPagina, IReadOnlyDictionary<string, string> extras,
        CancellationToken ct);

    /// <summary>Descarta a sessão em memória de uma instância (força novo login na próxima chamada).</summary>
    void Reiniciar(string provedor);
}

public sealed partial class KlinikosWebSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<KlinikosWebSessao> logger) : IKlinikosWebSessao, IDisposable
{
    /// <summary>Magic OLE2/BIFF — todo XLS do Crystal começa com estes 8 bytes.</summary>
    private static readonly byte[] MagicOle2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private const string PrefControles = "ctl00$ctl00$contentCenter$contentCenterChild$";

    /// <summary>
    /// Verbos que denunciam ESCRITA no nome do parâmetro ou no <c>__EVENTTARGET</c>. Copiado do
    /// laboratório (<c>klinikos/client.py</c>), já corrigido para NÃO casar tokens ingleses curtos
    /// dentro de palavras pt-BR ("save" em "responsavel"). A guarda real é o <c>__EVENTTARGET</c>.
    /// </summary>
    [GeneratedRegex(
        "(salvar|gravar|confirmar|inserir|incluir|excluir|remover|deletar|apagar|cancelar|"
        + "agendar|marcar|desmarcar|autorizar|devolver|encaminhar|executar|efetivar|finalizar|"
        + "aprovar|reprovar|transferir|submeter|registrar|atualizar|editar)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexEscrita();

    private readonly ConcurrentDictionary<string, Sessao> _sessoes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    private SemaphoreSlim Gate(string provedor) =>
        _gates.GetOrAdd(provedor, _ => new SemaphoreSlim(1, 1));

    // ------------------------------------------------------------------ público

    public async Task<byte[]> BaixarRelatorioXlsAsync(
        string provedor, string queryRelatorio, CancellationToken ct)
    {
        var gate = Gate(provedor);
        await gate.WaitAsync(ct);
        try
        {
            var sessao = await GarantirSessaoAsync(provedor, ct);
            var caminho = $"{sessao.Instancia.AppRoot}/Relatorios/{queryRelatorio}";
            var (bytes, _) = await GetBytesAsync(sessao, caminho, ct);

            if (EhBiff(bytes)) return bytes;

            // Não é BIFF: quase sempre a sessão morreu e voltou a tela de login. Relogar uma vez.
            var texto = Encoding.UTF8.GetString(bytes);
            if (KlinikosHtmlParser.EhTelaDeLogin(texto))
            {
                logger.LogWarning("Klinikos[{Prov}]: sessão morta ao baixar relatório — reautenticando.", provedor);
                Reiniciar(provedor);
                sessao = await GarantirSessaoAsync(provedor, ct);
                (bytes, _) = await GetBytesAsync(sessao, caminho, ct);
                if (EhBiff(bytes)) return bytes;
                texto = Encoding.UTF8.GetString(bytes);
            }

            throw new ValidacaoException(
                "klinikos.relatorio_nao_xls",
                $"O Klinikos não devolveu um XLS para o relatório ({provedor}). Resposta: "
                + Resumo(texto));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> AbrirTelaAsync(string provedor, string caminhoRelativo, CancellationToken ct)
    {
        var gate = Gate(provedor);
        await gate.WaitAsync(ct);
        try
        {
            var sessao = await GarantirSessaoAsync(provedor, ct);
            var caminho = $"{sessao.Instancia.AppRoot}/{caminhoRelativo.TrimStart('/')}";
            var (html, _) = await GetTextoAsync(sessao, caminho, ct);
            return html;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> SubmeterLeituraAsync(
        string provedor, string htmlPagina, IReadOnlyDictionary<string, string> extras,
        CancellationToken ct)
    {
        GarantirLeitura(extras);
        var gate = Gate(provedor);
        await gate.WaitAsync(ct);
        try
        {
            var sessao = await GarantirSessaoAsync(provedor, ct);
            var doc = KlinikosHtmlParser.Documento(htmlPagina);
            var form = KlinikosHtmlParser.PrimeiroForm(doc)
                ?? throw new ValidacaoException("klinikos.pagina_sem_form", "A página não tem form para submeter.");
            var campos = KlinikosHtmlParser.CamposDoForm(form);
            foreach (var (k, v) in extras) campos[k] = v;
            var action = ResolverAction(sessao, doc, form);
            var (bytes, _) = await PostAsync(sessao, action, campos, ct);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Reiniciar(string provedor)
    {
        if (_sessoes.TryRemove(provedor, out var s)) s.Dispose();
    }

    // ------------------------------------------------------------------ trava de leitura

    /// <summary>Recusa POST cujo nome de parâmetro OU valor de <c>__EVENTTARGET</c> tenha verbo de
    /// escrita. Login/gate não passam por aqui (porta interna de configuração de sessão).</summary>
    internal static void GarantirLeitura(IReadOnlyDictionary<string, string> extras)
    {
        foreach (var (chave, valor) in extras)
        {
            if (RegexEscrita().IsMatch(chave))
            {
                throw new EscritaNoKlinikosBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: o parâmetro '{chave}' parece escrita.");
            }
            if (chave == "__EVENTTARGET" && !string.IsNullOrEmpty(valor) && RegexEscrita().IsMatch(valor))
            {
                throw new EscritaNoKlinikosBloqueadaException(
                    $"POST recusado pela trava de somente-leitura: __EVENTTARGET '{valor}' parece escrita.");
            }
        }
    }

    // ------------------------------------------------------------------ sessão / login / gate

    private async Task<Sessao> GarantirSessaoAsync(string provedor, CancellationToken ct)
    {
        if (_sessoes.TryGetValue(provedor, out var existente) && existente.Pronta) return existente;

        var (instancia, usuario, senha) = await CarregarAsync(provedor, ct);
        Reiniciar(provedor);
        var sessao = new Sessao(instancia) { Credenciais = (usuario, senha) };
        _sessoes[provedor] = sessao;

        await LoginAsync(sessao, ct);
        await SelecionarLocalAsync(sessao, ct);
        sessao.Pronta = true;
        return sessao;
    }

    private async Task LoginAsync(Sessao sessao, CancellationToken ct)
    {
        var app = sessao.Instancia.AppRoot;
        var (html, url) = await GetTextoAsync(sessao, $"{app}/Login.aspx?ReturnUrl={Uri.EscapeDataString(app + "/")}", ct);
        var doc = KlinikosHtmlParser.Documento(html);
        var form = KlinikosHtmlParser.PrimeiroForm(doc)
            ?? throw new ValidacaoException("klinikos.login_sem_form", "A tela de login do Klinikos veio sem form.");
        if (!KlinikosHtmlParser.EhTelaDeLogin(html))
        {
            throw new ValidacaoException("klinikos.login_inesperado", "A tela de login do Klinikos mudou de layout.");
        }

        var (usuario, senha) = sessao.Credenciais;
        var campos = KlinikosHtmlParser.CamposDoForm(form);
        campos["LoginView1$lgAcesso$UserName"] = usuario;
        campos["LoginView1$lgAcesso$Password"] = senha;
        campos["LoginView1$lgAcesso$LoginButton"] = "ENTRAR";

        var action = ResolverAction(sessao, doc, form, url);
        var (bytes, urlPos) = await PostAsync(sessao, action, campos, ct);
        var resposta = Encoding.UTF8.GetString(bytes);

        // Sessão em outra estação: confirmar nesta (derruba a outra — esperado, usuário dedicado).
        if (KlinikosHtmlParser.PedeConfirmacaoDeSessao(resposta))
        {
            logger.LogInformation("Klinikos[{Prov}]: usuário logado em outra estação — confirmando nesta.", sessao.Instancia.Provedor);
            var docConf = KlinikosHtmlParser.Documento(resposta);
            var formConf = KlinikosHtmlParser.PrimeiroForm(docConf)
                ?? throw new ValidacaoException("klinikos.confirma_sem_form", "A confirmação de sessão veio sem form.");
            var camposConf = KlinikosHtmlParser.CamposDoForm(formConf);
            camposConf["btnConfirmarLogin"] = "CONFIRMA";
            var actionConf = ResolverAction(sessao, docConf, formConf, urlPos);
            (bytes, _) = await PostAsync(sessao, actionConf, camposConf, ct);
            resposta = Encoding.UTF8.GetString(bytes);
        }

        if (KlinikosHtmlParser.EhTelaDeLogin(resposta))
        {
            throw new ValidacaoException(
                "klinikos.login_falhou",
                $"Login no Klinikos falhou ({sessao.Instancia.Provedor}). Confira usuário/senha da credencial.");
        }
    }

    /// <summary>
    /// Gate do local de atendimento: após o login, toda tela protegida redireciona para
    /// <c>GravaCookie.aspx</c> até escolher o local. O checkbox de acesso administrativo tem
    /// AutoPostBack, então o navegador faz 2 POSTs — 1) o postback do checkbox, 2) o Salvar
    /// (ImageButton). Grava os cookies <c>_codigo_unidade</c>/<c>_LOCAL_NAME</c>.
    /// </summary>
    private async Task SelecionarLocalAsync(Sessao sessao, CancellationToken ct)
    {
        var app = sessao.Instancia.AppRoot;
        var (html, url) = await GetTextoAsync(sessao, $"{app}/Default.aspx", ct);
        if (!url.Contains("GravaCookie", StringComparison.OrdinalIgnoreCase)) return; // já tem local

        var doc = KlinikosHtmlParser.Documento(html);
        var form = KlinikosHtmlParser.PrimeiroForm(doc)
            ?? throw new ValidacaoException("klinikos.gate_sem_form", "O gate de local veio sem form.");
        var nomeChk = KlinikosHtmlParser.NomePorSufixo(form, "ckbAcessoAdministrativo")
                      ?? PrefControles + "ckbAcessoAdministrativo";

        // 1) postback do checkbox (AutoPostBack), marcando acesso administrativo.
        var campos = KlinikosHtmlParser.CamposDoForm(form);
        campos["__EVENTTARGET"] = nomeChk;
        campos["__EVENTARGUMENT"] = string.Empty;
        campos[nomeChk] = "on";
        var action = ResolverAction(sessao, doc, form, url);
        var (bytes, url2) = await PostAsync(sessao, action, campos, ct);
        var html2 = Encoding.UTF8.GetString(bytes);

        // 2) Salvar (ImageButton imbSalvar → nome.x/.y).
        var doc2 = KlinikosHtmlParser.Documento(html2);
        var form2 = KlinikosHtmlParser.PrimeiroForm(doc2)
            ?? throw new ValidacaoException("klinikos.gate_sem_form", "O gate de local (passo 2) veio sem form.");
        var nomeSalvar = KlinikosHtmlParser.NomePorSufixo(form2, "imbSalvar") ?? PrefControles + "imbSalvar";
        var campos2 = KlinikosHtmlParser.CamposDoForm(form2);
        campos2[nomeChk] = "on";
        campos2[nomeSalvar + ".x"] = "10";
        campos2[nomeSalvar + ".y"] = "10";
        var action2 = ResolverAction(sessao, doc2, form2, url2);
        await PostAsync(sessao, action2, campos2, ct);

        var (_, urlFinal) = await GetTextoAsync(sessao, $"{app}/Default.aspx", ct);
        if (urlFinal.Contains("GravaCookie", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidacaoException(
                "klinikos.gate_nao_passou",
                $"Não foi possível definir o local de atendimento ({sessao.Instancia.Provedor}).");
        }
    }

    // ------------------------------------------------------------------ HTTP

    private static string ResolverAction(Sessao sessao, IHtmlDocument doc, IHtmlFormElement form, string? urlPagina = null)
    {
        var action = KlinikosHtmlParser.ActionDoForm(doc, form.Id ?? string.Empty) ?? form.GetAttribute("action");
        var baseUri = urlPagina is not null ? new Uri(urlPagina) : sessao.Instancia.BaseUri;
        return string.IsNullOrWhiteSpace(action) ? baseUri.ToString() : new Uri(baseUri, action).ToString();
    }

    private static async Task<(byte[] Bytes, string Url)> GetBytesAsync(Sessao sessao, string caminho, CancellationToken ct)
    {
        var uri = new Uri(sessao.Instancia.BaseUri, caminho);
        using var resposta = await sessao.Http.GetAsync(uri, ct);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(ct);
        return (bytes, (resposta.RequestMessage?.RequestUri ?? uri).ToString());
    }

    private static async Task<(string Html, string Url)> GetTextoAsync(Sessao sessao, string caminho, CancellationToken ct)
    {
        var (bytes, url) = await GetBytesAsync(sessao, caminho, ct);
        return (Encoding.UTF8.GetString(bytes), url);
    }

    private static async Task<(byte[] Bytes, string Url)> PostAsync(
        Sessao sessao, string url, IReadOnlyDictionary<string, string> campos, CancellationToken ct)
    {
        using var conteudo = new FormUrlEncodedContent(campos);
        var uri = new Uri(url);
        using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = conteudo };
        using var resposta = await sessao.Http.SendAsync(req, ct);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(ct);
        return (bytes, (resposta.RequestMessage?.RequestUri ?? uri).ToString());
    }

    private static bool EhBiff(byte[] b) => b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(MagicOle2);

    /// <summary>Resumo curto e sem tags do corpo, para a mensagem de erro (sem PII estruturada).</summary>
    private static string Resumo(string html)
    {
        var sb = new StringBuilder(html.Length);
        var dentroTag = false;
        var espaco = false;
        foreach (var c in html)
        {
            if (c == '<') { dentroTag = true; continue; }
            if (c == '>') { dentroTag = false; espaco = true; continue; }
            if (dentroTag) continue;
            if (char.IsWhiteSpace(c)) { espaco = true; continue; }
            if (espaco && sb.Length > 0) sb.Append(' ');
            espaco = false;
            sb.Append(c);
            if (sb.Length >= 120) break;
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ credencial

    private async Task<(KlinikosInstancia Instancia, string Usuario, string Senha)> CarregarAsync(
        string provedor, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(provedor, ct);
        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "klinikos.credencial_incompleta",
                $"Configure usuário e senha da instância Klinikos '{provedor}'.");
        }
        return (KlinikosInstancia.De(provedor, ctx.ParametrosJson), ctx.ClientId!, ctx.ClientSecret!);
    }

    // ------------------------------------------------------------------ sessão (estado)

    private sealed class Sessao : IDisposable
    {
        public Sessao(KlinikosInstancia instancia)
        {
            Instancia = instancia;
            Http = CriarHttp(instancia.BaseUri);
        }

        public KlinikosInstancia Instancia { get; }
        public HttpClient Http { get; }
        public bool Pronta { get; set; }

        /// <summary>Preenchido quando a sessão é criada (antes do login).</summary>
        public (string Usuario, string Senha) Credenciais { get; init; } = (string.Empty, string.Empty);

        private static HttpClient CriarHttp(Uri baseUri)
        {
            var handler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                AllowAutoRedirect = true,
            };
            var http = new HttpClient(handler) { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(120) };
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
        foreach (var s in _sessoes.Values) s.Dispose();
        foreach (var g in _gates.Values) g.Dispose();
    }
}

/// <summary>A trava de somente-leitura recusou a operação. É <b>bug do motor</b>, não do Klinikos.</summary>
public sealed class EscritaNoKlinikosBloqueadaException(string mensagem) : Exception(mensagem);
