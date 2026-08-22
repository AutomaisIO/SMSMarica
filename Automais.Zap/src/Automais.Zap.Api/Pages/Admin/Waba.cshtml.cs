using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Entregas;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Seguranca;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class WabaModel(
    ZapDbContext db,
    EscopoUsuario escopo,
    IGraphMetaClient graph,
    IConfiguracaoMetaService configuracao,
    IProtetorSegredos protetor,
    TimeProvider relogio) : PageModel
{
    public Data.Entities.Waba? Alvo { get; private set; }
    public List<Numero> Numeros { get; private set; } = [];
    public IReadOnlyList<AppInscrito> AppsInscritos { get; private set; } = [];
    public string? AppIdProprio { get; private set; }
    public string? ErroGraph { get; private set; }
    public bool Global => escopo.Global;

    public bool EsteAppInscrito => AppIdProprio is not null && AppsInscritos.Any(a => a.Id == AppIdProprio);
    public IEnumerable<AppInscrito> OutrosApps => AppsInscritos.Where(a => a.Id != AppIdProprio);

    /// <summary>Segredo em claro, exibido UMA vez logo apos gerar.</summary>
    [TempData] public string? SegredoNovo { get; set; }

    public bool TemSegredo => Alvo?.SegredoEntregaCifrado is { Length: > 0 };
    public bool TenantAtivo { get; private set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var permitido = await CarregarAsync(id, ct);
        if (!permitido) return Forbid();
        if (Alvo is null) return NotFound();

        var creds = await configuracao.ObterAsync(ct);
        AppIdProprio = creds.AppId;

        if (creds.PodeGerenciar)
        {
            var apps = await graph.ListarAppsInscritosAsync(Alvo.WabaId, ct);
            if (apps.Sucesso) AppsInscritos = apps.Valor!;
            else ErroGraph = apps.Erro;
        }

        return Page();
    }

    private async Task<bool> CarregarAsync(Guid id, CancellationToken ct)
    {
        Alvo = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (Alvo is null) return true;

        if (!await escopo.PodeVerAsync(Alvo.TenantId, ct)) return false;

        TenantAtivo = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == Alvo.TenantId && t.Ativo && t.SuspensoEm == null, ct);

        Numeros = await db.Numeros.AsNoTracking()
            .Where(n => n.WabaId == id)
            .OrderBy(n => n.DisplayPhoneNumber)
            .ToListAsync(ct);

        return true;
    }

    /// <summary>
    /// A URL vira destino de um POST feito PELO SERVIDOR. Sem isto, o operador aponta o relay
    /// para 127.0.0.1:5086, para a metadata da nuvem (169.254.169.254) ou para a rede
    /// interna, e o relay vira proxy de SSRF.
    /// </summary>
    private static bool UrlDestinoValida(string url, out string motivo)
    {
        motivo = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            motivo = "A URL de destino precisa ser https absoluta (o relay fala com a instância pela internet).";
            return false;
        }
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" || host.EndsWith(".local") || host.EndsWith(".internal"))
        {
            motivo = "Host de destino não permitido.";
            return false;
        }
        if (System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal
                || EhPrivadoV4(ip))
            {
                motivo = "IP de destino não permitido (loopback, link-local ou rede privada).";
                return false;
            }
        }
        return true;
    }

    private static bool EhPrivadoV4(System.Net.IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || b[0] == 127 || b[0] == 0;
    }

    private async Task<Data.Entities.Waba?> AutorizarAsync(Guid id, CancellationToken ct)
    {
        var waba = await db.Wabas.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return null;
        return await escopo.PodeVerAsync(waba.TenantId, ct) ? waba : null;
    }

    public async Task<IActionResult> OnPostRoteamentoAsync(
        Guid id, string? urlDestino, bool ativo, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        urlDestino = (urlDestino ?? "").Trim();
        if (urlDestino.Length > 0 && !UrlDestinoValida(urlDestino, out var motivoUrl))
        {
            Erro = motivoUrl;
            return RedirectToPage(new { id });
        }

        if (ativo && urlDestino.Length == 0)
        {
            Erro = "Não dá para ligar o roteamento sem destino — os eventos cairiam sem rota.";
            return RedirectToPage(new { id });
        }

        // Sem segredo proprio, a entrega sai com a assinatura da Meta — que a instancia nao
        // aceita mais. Ligar o roteamento assim so produziria 401 em serie.
        if (ativo && string.IsNullOrEmpty(waba.SegredoEntregaCifrado))
        {
            Erro = "Gere o segredo de entrega antes de ligar o roteamento — sem ele a instância recusa tudo com 401.";
            return RedirectToPage(new { id });
        }

        waba.UrlDestino = urlDestino.Length == 0 ? null : urlDestino;
        waba.RoteamentoAtivo = ativo;
        await db.SaveChangesAsync(ct);

        Recado = ativo
            ? "Roteamento ligado. Os números deste WABA passam a ser entregues nesse destino."
            : "Roteamento desligado. Os eventos deste WABA param de ser entregues.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGerarSegredoAsync(Guid id, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        var segredo = AssinaturaAutomais.GerarSegredo();
        waba.SegredoEntregaCifrado = protetor.Proteger(segredo);
        await db.SaveChangesAsync(ct);

        SegredoNovo = segredo;
        Recado = "Segredo gerado. Copie agora e configure na aplicação de destino — enquanto ela "
                 + "não conhecer esse valor, vai recusar tudo que mandarmos.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoverSegredoAsync(Guid id, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        waba.SegredoEntregaCifrado = null;
        waba.RoteamentoAtivo = false;
        await db.SaveChangesAsync(ct);
        Recado = "Segredo removido e roteamento desligado. Gere um segredo novo, configure no destino e religue.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSincronizarAsync(Guid id, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        var r = await graph.ListarNumerosAsync(waba.WabaId, ct);
        if (!r.Sucesso)
        {
            Erro = "A Meta recusou: " + r.Erro;
            return RedirectToPage(new { id });
        }

        var existentes = await db.Numeros.Where(n => n.WabaId == id).ToListAsync(ct);
        var novos = 0;

        foreach (var vindo in r.Valor!)
        {
            var atual = existentes.FirstOrDefault(n => n.PhoneNumberId == vindo.Id);
            if (atual is null)
            {
                // O índice único de phone_number_id é global: se o número já estiver noutro
                // tenant, é engano de cadastro e tem de aparecer, não ser engolido.
                if (await db.Numeros.AnyAsync(n => n.PhoneNumberId == vindo.Id, ct))
                {
                    Erro = escopo.Global
                        ? $"O número {vindo.DisplayPhoneNumber ?? vindo.Id} já está cadastrado em outro WABA."
                        : "Um dos números não pôde ser importado. Fale com a plataforma.";
                    continue;
                }

                db.Numeros.Add(new Numero
                {
                    WabaId = id,
                    PhoneNumberId = vindo.Id,
                    DisplayPhoneNumber = vindo.DisplayPhoneNumber,
                    Rotulo = vindo.VerifiedName,
                    Ativo = true,
                    CriadoEm = relogio.GetUtcNow(),
                });
                novos++;
            }
            else
            {
                atual.DisplayPhoneNumber = vindo.DisplayPhoneNumber;
                atual.Rotulo = vindo.VerifiedName;
                atual.AtualizadoEm = relogio.GetUtcNow();
            }
        }

        waba.SincronizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        Recado = $"Sincronizado com a Meta: {novos} número(s) novo(s), {r.Valor!.Count} no total.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAlternarNumeroAsync(Guid id, Guid numeroId, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        var numero = await db.Numeros.FirstOrDefaultAsync(n => n.Id == numeroId && n.WabaId == id, ct);
        if (numero is null) return NotFound();

        numero.Ativo = !numero.Ativo;
        numero.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostOverrideAsync(Guid id, Guid numeroId, string? url, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        url = (url ?? "").Trim();
        if (url.Length > 0 && !UrlDestinoValida(url, out var motivoUrl))
        {
            Erro = motivoUrl;
            return RedirectToPage(new { id });
        }

        var numero = await db.Numeros.FirstOrDefaultAsync(n => n.Id == numeroId && n.WabaId == id, ct);
        if (numero is null) return NotFound();

        numero.UrlDestinoOverride = url.Length == 0 ? null : url;
        numero.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        Recado = url.Length == 0 ? "Número voltou a herdar o destino do WABA." : "Destino próprio do número salvo.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostInscreverAsync(Guid id, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        var r = await graph.InscreverAppNoWabaAsync(waba.WabaId, ct);
        if (r.Sucesso) Recado = "App inscrito. A Meta passa a entregar os eventos deste WABA no nosso webhook.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDesinscreverAsync(Guid id, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        var r = await graph.DesinscreverAppDoWabaAsync(waba.WabaId, ct);
        if (r.Sucesso) Recado = "App desinscrito. Este WABA para de entregar eventos aqui.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }
}
