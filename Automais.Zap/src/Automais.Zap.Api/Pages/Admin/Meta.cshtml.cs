using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Relay;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class MetaModel(
    IConfiguracaoMetaService configuracao,
    IGraphMetaClient graph,
    IOptions<RelayOptions> opcoes) : PageModel
{
    /// <summary>Campos que a Meta manda para o WhatsApp. É o que o relay sabe rotear.</summary>
    public static readonly string[] CamposWebhook = ["messages", "message_template_status_update"];

    public string? AppId { get; private set; }
    public bool TemAppSecret { get; private set; }
    public bool TemVerifyToken { get; private set; }
    public bool TemTokenSistema { get; private set; }
    public string BaseUrl { get; private set; } = "";
    public string UrlWebhookSugerida { get; private set; } = "";

    public IReadOnlyList<AssinaturaWebhook> Assinaturas { get; private set; } = [];
    public string? ErroGraph { get; private set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CarregarAsync(ct);

    private async Task CarregarAsync(CancellationToken ct)
    {
        var creds = await configuracao.ObterAsync(ct);
        AppId = creds.AppId;
        TemAppSecret = !string.IsNullOrWhiteSpace(creds.AppSecret);
        TemVerifyToken = !string.IsNullOrWhiteSpace(creds.VerifyToken);
        TemTokenSistema = !string.IsNullOrWhiteSpace(creds.TokenSistema);
        BaseUrl = creds.BaseUrl;
        UrlWebhookSugerida = opcoes.Value.UrlWebhookPublica ?? "";

        if (creds.TokenApp is not null)
        {
            var r = await graph.ObterWebhookDoAppAsync(ct);
            if (r.Sucesso) Assinaturas = r.Valor!;
            else ErroGraph = r.Erro;
        }
    }

    public async Task<IActionResult> OnPostSalvarAsync(
        string? appId, string? appSecret, string? verifyToken, string? tokenSistema, string? baseUrl,
        CancellationToken ct)
    {
        await configuracao.SalvarAsync(
            new AtualizarConfiguracaoMeta(appId, appSecret, verifyToken, tokenSistema, baseUrl), ct);
        Recado = "Credenciais salvas. Campos de segredo em branco foram mantidos como estavam.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfigurarWebhookAsync(string urlWebhook, CancellationToken ct)
    {
        urlWebhook = (urlWebhook ?? "").Trim();
        if (!Uri.TryCreate(urlWebhook, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            // A Meta recusa callback que não seja https — melhor barrar aqui do que gastar
            // uma ida à Graph API para ouvir isso dela.
            Erro = "A Callback URL precisa ser https absoluta.";
            return RedirectToPage();
        }

        var r = await graph.ConfigurarWebhookDoAppAsync(urlWebhook, CamposWebhook, ct);
        if (r.Sucesso)
        {
            Recado = $"Webhook do App apontado para {urlWebhook}. A Meta chamou a URL e o verify token conferiu.";
        }
        else
        {
            Erro = "A Meta recusou: " + r.Erro;
        }

        return RedirectToPage();
    }
}
