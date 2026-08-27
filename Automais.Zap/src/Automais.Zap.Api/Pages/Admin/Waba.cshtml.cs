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

        // Chegar por URL a um WABA de outro tenant realinha o contexto do painel inteiro.
        escopo.Selecionar(Alvo.TenantId);

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
    ///
    /// O host também é RESOLVIDO aqui: um nome DNS apontando para IP privado é o mesmo
    /// ataque com um passo a mais. A checagem é no salvamento — não elimina rebinding
    /// posterior, mas fecha o caminho barato.
    /// </summary>
    private static async Task<(bool Ok, string Motivo)> UrlDestinoValidaAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return (false, "A URL de destino precisa ser https absoluta (a plataforma fala com o sistema de destino pela internet).");
        }
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" || host.EndsWith(".local") || host.EndsWith(".internal"))
        {
            return (false, "Host de destino não permitido.");
        }

        if (System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var ipLiteral))
        {
            return IpProibido(ipLiteral)
                ? (false, "IP de destino não permitido (loopback, link-local ou rede privada).")
                : (true, "");
        }

        try
        {
            var ips = await System.Net.Dns.GetHostAddressesAsync(host, ct);
            if (ips.Length == 0)
            {
                return (false, "O host de destino não resolve para nenhum endereço.");
            }
            if (ips.Any(IpProibido))
            {
                return (false, "O host de destino resolve para um endereço interno (loopback, link-local ou rede privada).");
            }
        }
        catch (Exception)
        {
            // Fail-closed: destino que não resolve agora não vira rota; salvar de novo quando o DNS existir.
            return (false, "Não foi possível resolver o host de destino agora — confira o endereço e tente de novo.");
        }

        return (true, "");
    }

    private static bool IpProibido(System.Net.IPAddress ip)
        => System.Net.IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || EhPrivadoV4(ip);

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
        if (urlDestino.Length > 0)
        {
            var (ok, motivoUrl) = await UrlDestinoValidaAsync(urlDestino, ct);
            if (!ok)
            {
                Erro = motivoUrl;
                return RedirectToPage(new { id });
            }
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
            Erro = "Gere o segredo de entrega antes de ligar o roteamento — sem ele o destino recusa tudo com 401.";
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
        if (url.Length > 0)
        {
            var (ok, motivoUrl) = await UrlDestinoValidaAsync(url, ct);
            if (!ok)
            {
                Erro = motivoUrl;
                return RedirectToPage(new { id });
            }
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
