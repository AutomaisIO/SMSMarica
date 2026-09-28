namespace SMSMais.Integrador.Agente;

/// <summary>
/// Um sistema observado. Espelha o <c>SITIOS</c> da extensão (<c>SMSMais.chrome/config.js</c>):
/// <c>Blur</c> bloqueia o site até o login no SMSMais; <c>Modo</c> = "minimo" (só comando + nº da
/// solicitação, sem PII) ou "analise" (captura bruta: envio + retorno).
/// </summary>
public sealed record Sitio(string Id, string Label, string Host, bool Blur, string Modo);

/// <summary>Configuração do agente. Os padrões apontam para o backend LOCAL; produção é opt-in.</summary>
public sealed class Config
{
    /// <summary>Para onde as capturas vão. Sem barra no fim.</summary>
    public string ApiBase { get; init; } = "http://localhost:5080";

    /// <summary>Origem do painel do SMSMais, de onde o agente lê a sessão (login).</summary>
    public string PainelOrigin { get; init; } = "https://smsmarica.online";

    /// <summary>Primeira página aberta no Chrome do agente.</summary>
    public string UrlInicial { get; init; } = "https://sisregiii.saude.gov.br/";

    /// <summary>Navegador: "chrome" (padrão) ou "edge".</summary>
    public string Navegador { get; init; } = "chrome";

    /// <summary>Loga cada captura (kind + caminho) — para depurar a PoC sem olhar a tela.</summary>
    public bool Verboso { get; init; }

    // Envio em lote (mesmos números da extensão).
    public int LoteMaxItens { get; init; } = 40;
    public int LoteIntervaloMs { get; init; } = 4000;
    public int LoteMaxBytes { get; init; } = 3_000_000;
    public int RespostaMaxChars { get; init; } = 2_000_000;
    public int BufferMax { get; init; } = 5000;

    public const string RotaCapturas = "/extensao/sisreg/capturas";

    /// <summary>Vai no lote como <c>versao</c> — o <c>/capturas/resumo</c> distingue o canal da extensão.</summary>
    public string Versao { get; } =
        "integrador-" + (typeof(Config).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    public string MarcaNomePadrao => "SMSMarica";
    public string MarcaCorPadrao => "#C8102E";

    public IReadOnlyList<Sitio> Sitios { get; init; } =
    [
        new("sisreg", "SISREG", "sisregiii.saude.gov.br", Blur: true, Modo: "minimo"),
        new("ecosistemas", "Ecossistemas Maricá", "marica.ecosistemas.com.br", Blur: false, Modo: "analise"),
    ];

    /// <summary>Pasta por usuário (sem admin): perfil do Chrome, installId, log.</summary>
    public string PastaDados { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMSMais", "integrador");

    public string PastaPerfil => Path.Combine(PastaDados, Navegador);

    public string HostPainel
    {
        get
        {
            try { return new Uri(PainelOrigin).Host; }
            catch { return string.Empty; }
        }
    }

    public Sitio? SitioDeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
            var host = new Uri(url).Host;
            return Sitios.FirstOrDefault(s => string.Equals(s.Host, host, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Linha de comando: <c>--api URL</c>, <c>--painel URL</c>, <c>--url URL</c>, <c>--edge</c>.
    /// Também aceita as variáveis SMSMAIS_API_BASE e SMSMAIS_PAINEL_ORIGIN.
    /// </summary>
    public static Config DeArgs(string[] args)
    {
        var api = Environment.GetEnvironmentVariable("SMSMAIS_API_BASE");
        var painel = Environment.GetEnvironmentVariable("SMSMAIS_PAINEL_ORIGIN");
        string? url = null;
        var navegador = "chrome";
        var verboso = false;

        for (var i = 0; i < args.Length; i++)
        {
            var atual = args[i];
            var prox = i + 1 < args.Length ? args[i + 1] : null;
            switch (atual)
            {
                case "--api": api = prox; i++; break;
                case "--painel": painel = prox; i++; break;
                case "--url": url = prox; i++; break;
                case "--edge": navegador = "edge"; break;
                case "--verboso": verboso = true; break;
            }
        }

        var padrao = new Config();
        return new Config
        {
            ApiBase = (api ?? padrao.ApiBase).TrimEnd('/'),
            PainelOrigin = (painel ?? padrao.PainelOrigin).TrimEnd('/'),
            UrlInicial = url ?? padrao.UrlInicial,
            Navegador = navegador,
            Verboso = verboso,
        };
    }
}
