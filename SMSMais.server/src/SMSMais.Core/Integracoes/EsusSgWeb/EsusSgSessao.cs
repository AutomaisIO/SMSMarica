using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;

namespace SMSMais.Core.Integracoes.EsusSgWeb;

/// <summary>
/// Sessão autenticada no <b>ESUS de São Gonçalo</b> ("Novo Esus", <c>saogoncalo.esusmais.com.br</c>)
/// — o produto ESUS, <b>não</b> o e-SUS do governo (ADR-0063). Protocolo medido em
/// <c>Automais.esus_saocongalo/docs/APRENDIZADOS.md</c> (30/09/2026).
///
/// <para><b>Não é scraping de HTML.</b> O front do ESUS é um SPA Vue que fala JSON com dois
/// backends: o novo (Node, <c>:8001</c>, REST + GraphQL) e o legado (PHP, <c>:9001</c>), onde moram
/// as telas de fila e agendados. O login é duplo e cada backend tem seu token:</para>
/// <list type="number">
/// <item><c>POST :8001/access-control/login-light</c> <c>{namespaces, client, username, password}</c>;</item>
/// <item><c>POST :9001/niveisacesso/login-sem-permissoes</c> <c>{usuario, senha, cliente}</c>.</item>
/// </list>
/// <para>Toda chamada autenticada leva <c>authorization: &lt;token&gt;</c> (UUID cru, sem
/// "Bearer") e <c>unithealth: &lt;uns_id&gt;</c>. No legado o token é o do legado. Sessão de 3600 s.</para>
///
/// <para><b>SOMENTE LEITURA.</b> O legado usa POST para tudo, então a trava é pelo NOME da ação
/// (último segmento do caminho): só passa ação que começa por verbo de leitura e não embute verbo
/// de escrita — conferida contra as 736 ações que o front conhece. Não há porta de escrita.</para>
///
/// <para><b>Sessão única</b>, serializada por semáforo. HTTP 401 = sessão vencida: refaz o login
/// uma vez e repete.</para>
/// </summary>
public interface IEsusSgSessao
{
    /// <summary>POST no legado no formato do front (<c>{"arrFormData": form, ...extras}</c>).
    /// Devolve o <c>dados</c> da resposta; <c>status:false</c> vira <see cref="EsusSgRespostaErroException"/>.</summary>
    Task<JsonElement> PostarLegadoAsync(
        string caminho, JsonObject corpo, CancellationToken cancellationToken);

    /// <summary>Unidade de saúde da conta no ESUS (a "MUNICÍPIO DE MARICÁ", uns_id 39 em 30/09/2026).</summary>
    Task<int> UnidadeAsync(CancellationToken cancellationToken);

    /// <summary>Loga com uma credencial avulsa (teste antes de salvar). Devolve o nome do usuário.</summary>
    Task<string> AutenticarAvulsoAsync(
        string usuario, string senha, string? cliente, CancellationToken cancellationToken);

    void Reiniciar();
}

/// <summary>A trava de somente-leitura recusou a chamada. É bug do motor, não do ESUS.</summary>
public sealed class EscritaNoEsusSgBloqueadaException(string mensagem) : Exception(mensagem);

/// <summary>O ESUS respondeu <c>status:false</c> (ex.: "Usuário não possui permissão").</summary>
public sealed class EsusSgRespostaErroException(string mensagem) : Exception(mensagem);

public sealed partial class EsusSgSessao(
    IServiceScopeFactory scopeFactory,
    ILogger<EsusSgSessao> logger) : IEsusSgSessao, IDisposable
{
    public const string Provedor = "esussg";
    public const string ClientePadrao = "SGO";
    public const string ApiUrlPadrao = "https://saogoncalo.esusmais.com.br:8001";
    public const string LegadoUrlPadrao = "https://saogoncalo.esusmais.com.br:9001";
    private const string FrontUrl = "https://saogoncalo.esusmais.com.br:8000";

    /// <summary>O login-light devolve as permissões DESTES namespaces; o acesso aos dados não
    /// depende deles (medido: lista vazia e lista mínima dão a mesma fila). Mandamos só o que o
    /// motor usa, para a resposta ficar legível no diagnóstico.</summary>
    private static readonly string[] Namespaces =
    [
        "consulta.filaConsulta", "consulta.buscaPacientesAgendadosFilaConsulta",
        "exame2.filaExame", "exame2.buscaPacientesAgendadosFilaExames",
    ];

    [GeneratedRegex(
        "^(buscar|listar|pesquisar|consultar|obter|carregar|exibir|visualizar|verificar|combo|combobox)"
        + "([-_a-z0-9]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexAcaoLeitura();

    /// <summary><c>comprovante</c>: gerar o comprovante pode carimbar "comprovante impresso" no
    /// agendamento — efeito colateral no SG, então fica fora mesmo começando com "obter".</summary>
    [GeneratedRegex(
        "(salvar|gravar|incluir|inserir|alterar|editar|atualizar|excluir|remover|deletar|apagar|"
        + "cancelar|agendar|desagendar|marcar|desmarcar|transferir|efetivar|confirmar|regular|mudar|"
        + "resolver|cadastrar|enviar|imprimir|unificar|inativar|ativar|habilitar|comprovante|"
        + "importar|exportar|gerar|adicionar|avancar|recuar|vincular)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RegexEscrita();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Conexao? _conexao;

    public void Reiniciar()
    {
        _conexao?.Dispose();
        _conexao = null;
    }

    public void Dispose() => Reiniciar();

    /// <summary>Trava de somente-leitura. Pública para os testes provarem que ela recusa.</summary>
    public static void GarantirLeitura(string caminho)
    {
        var acao = caminho.TrimEnd('/').Split('/').LastOrDefault() ?? "";
        if (!RegexAcaoLeitura().IsMatch(acao) || RegexEscrita().IsMatch(acao))
        {
            throw new EscritaNoEsusSgBloqueadaException(
                $"TRAVA: ação do ESUS recusada (não é leitura): {caminho}");
        }
    }

    public async Task<int> UnidadeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var c = await GarantirConexaoAsync(cancellationToken);
            return c.Unidade;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JsonElement> PostarLegadoAsync(
        string caminho, JsonObject corpo, CancellationToken cancellationToken)
    {
        GarantirLeitura(caminho);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            for (var tentativa = 1; ; tentativa++)
            {
                var c = await GarantirConexaoAsync(cancellationToken);
                using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(c.LegadoUri, caminho.TrimStart('/')))
                {
                    Content = JsonContent.Create(corpo),
                };
                req.Headers.TryAddWithoutValidation("authorization", c.TokenLegado);
                req.Headers.TryAddWithoutValidation("unithealth", c.Unidade.ToString());

                using var resp = await c.Http.SendAsync(req, cancellationToken);
                if (resp.StatusCode == HttpStatusCode.Unauthorized && tentativa == 1)
                {
                    logger.LogInformation("ESUS SG: sessão vencida (401) em {Caminho}; refazendo o login.", caminho);
                    Reiniciar();
                    continue;
                }

                var texto = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"ESUS SG {caminho}: HTTP {(int)resp.StatusCode} — {Recortar(texto)}");
                }

                using var doc = JsonDocument.Parse(texto);
                var raiz = doc.RootElement;
                if (raiz.ValueKind == JsonValueKind.Object
                    && raiz.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.False)
                {
                    var msg = raiz.TryGetProperty("trace", out var tr) && tr.ValueKind == JsonValueKind.Array
                              && tr.GetArrayLength() > 0 && tr[0].ValueKind == JsonValueKind.String
                        ? tr[0].GetString()
                        : raiz.TryGetProperty("dados", out var dd) && dd.ValueKind == JsonValueKind.String
                            ? dd.GetString()
                            : "status:false";
                    throw new EsusSgRespostaErroException($"ESUS SG {caminho}: {msg}");
                }

                var dados = raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("dados", out var d)
                    ? d
                    : raiz;
                return dados.Clone();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> AutenticarAvulsoAsync(
        string usuario, string senha, string? cliente, CancellationToken cancellationToken)
    {
        var cred = new Credenciais(
            usuario, senha, string.IsNullOrWhiteSpace(cliente) ? ClientePadrao : cliente.Trim(),
            new Uri(ApiUrlPadrao + "/"), new Uri(LegadoUrlPadrao + "/"));
        try
        {
            using var c = await LogarAsync(cred, cancellationToken);
            return c.NomeUsuario;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ValidacaoException)
        {
            throw new ValidacaoException("esussg.login_falhou", $"O ESUS recusou o login: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ login

    private async Task<Conexao> GarantirConexaoAsync(CancellationToken cancellationToken)
    {
        if (_conexao is { } c && c.ValidaAte > DateTime.UtcNow) return c;
        Reiniciar();
        var cred = await CarregarCredenciaisAsync(cancellationToken);
        _conexao = await LogarAsync(cred, cancellationToken);
        logger.LogInformation("ESUS SG: sessão aberta (unidade {Unidade}).", _conexao.Unidade);
        return _conexao;
    }

    private static async Task<Conexao> LogarAsync(Credenciais cred, CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        // O legado demora: a busca de agendados de um ano antigo levou 60 s medidos.
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", FrontUrl);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", FrontUrl + "/");
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) SMSMais");

        try
        {
            var cliente = cred.Cliente.ToLowerInvariant();

            using var rNovo = await http.PostAsJsonAsync(
                new Uri(cred.ApiUri, "access-control/login-light"),
                new { namespaces = Namespaces, client = cliente, username = cred.Usuario, password = cred.Senha },
                cancellationToken);
            var tNovo = await rNovo.Content.ReadAsStringAsync(cancellationToken);
            using var dNovo = JsonDocument.Parse(string.IsNullOrWhiteSpace(tNovo) ? "{}" : tNovo);
            if (!rNovo.IsSuccessStatusCode
                || !dNovo.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("token", out var tok) || string.IsNullOrWhiteSpace(tok.GetString()))
            {
                throw new ValidacaoException(
                    "esussg.login_falhou",
                    $"O ESUS recusou o login (HTTP {(int)rNovo.StatusCode}). Confira usuário, senha e cliente.");
            }

            var user = data.GetProperty("user");
            var unidade = user.TryGetProperty("usu_id_unidades_saude_padrao", out var u) && u.ValueKind == JsonValueKind.Number
                ? u.GetInt32()
                : throw new ValidacaoException("esussg.sem_unidade", "O usuário do ESUS não tem unidade padrão.");
            var nome = user.TryGetProperty("usu_nome", out var n) ? n.GetString() ?? cred.Usuario : cred.Usuario;
            var expira = data.TryGetProperty("loginInfo", out var li) && li.TryGetProperty("expireTime", out var et)
                         && et.ValueKind == JsonValueKind.Number
                ? et.GetInt32()
                : 3600;

            using var rLeg = await http.PostAsJsonAsync(
                new Uri(cred.LegadoUri, "niveisacesso/login-sem-permissoes"),
                new { usuario = cred.Usuario, senha = cred.Senha, cliente },
                cancellationToken);
            var tLeg = await rLeg.Content.ReadAsStringAsync(cancellationToken);
            using var dLeg = JsonDocument.Parse(string.IsNullOrWhiteSpace(tLeg) ? "{}" : tLeg);
            var tokenLegado = LerTokenLegado(dLeg.RootElement)
                ?? throw new ValidacaoException(
                    "esussg.login_legado_falhou",
                    $"O backend legado do ESUS não devolveu token (HTTP {(int)rLeg.StatusCode}).");

            // Renova com folga: 5 min antes do fim declarado pelo ESUS.
            var validaAte = DateTime.UtcNow.AddSeconds(Math.Max(300, expira - 300));
            return new Conexao(http, cred.LegadoUri, tokenLegado, unidade, nome, validaAte);
        }
        catch
        {
            http.Dispose();
            throw;
        }
    }

    private static string? LerTokenLegado(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object) return null;
        if (raiz.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String) return t.GetString();
        if (raiz.TryGetProperty("dados", out var d) && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("token", out var t2) && t2.ValueKind == JsonValueKind.String) return t2.GetString();
        return null;
    }

    private async Task<Credenciais> CarregarCredenciaisAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var credenciais = scope.ServiceProvider.GetRequiredService<IIntegracaoCredencialService>();
        var ctx = await credenciais.ObterContextoAsync(Provedor, cancellationToken);

        if (string.IsNullOrWhiteSpace(ctx.ClientId) || string.IsNullOrWhiteSpace(ctx.ClientSecret))
        {
            throw new ValidacaoException(
                "esussg.credencial_incompleta",
                "Configure o usuário e a senha do ESUS em Regulação → ESUS SG → Configuração.");
        }

        var p = ParametrosEsusSg.Ler(ctx.ParametrosJson);
        return new Credenciais(ctx.ClientId!, ctx.ClientSecret!, p.Cliente,
            new Uri(p.ApiUrl.TrimEnd('/') + "/"), new Uri(p.LegadoUrl.TrimEnd('/') + "/"));
    }

    private static string Recortar(string s) => s.Length <= 300 ? s : s[..300] + "…";

    private sealed record Credenciais(string Usuario, string Senha, string Cliente, Uri ApiUri, Uri LegadoUri);

    private sealed record Conexao(
        HttpClient Http, Uri LegadoUri, string TokenLegado, int Unidade, string NomeUsuario, DateTime ValidaAte)
        : IDisposable
    {
        public void Dispose() => Http.Dispose();
    }
}

/// <summary>O que mora no <c>ParametrosJson</c> da credencial <c>esussg</c>.</summary>
public sealed record ParametrosEsusSg(string Cliente, string ApiUrl, string LegadoUrl)
{
    public static ParametrosEsusSg Ler(string? json)
    {
        string? cliente = null, api = null, legado = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                cliente = Texto(r, "cliente");
                api = Texto(r, "apiUrl");
                legado = Texto(r, "legadoUrl");
            }
            catch (JsonException)
            {
                // Parâmetro corrompido não derruba o motor: cai nos padrões medidos.
            }
        }

        return new ParametrosEsusSg(
            cliente ?? EsusSgSessao.ClientePadrao,
            api ?? EsusSgSessao.ApiUrlPadrao,
            legado ?? EsusSgSessao.LegadoUrlPadrao);
    }

    private static string? Texto(JsonElement r, string chave) =>
        r.ValueKind == JsonValueKind.Object && r.TryGetProperty(chave, out var v)
        && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
