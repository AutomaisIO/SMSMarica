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

        Numeros = await db.Numeros.AsNoTracking()
            .Where(n => n.WabaId == id)
            .OrderBy(n => n.DisplayPhoneNumber)
            .ToListAsync(ct);

        return true;
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
        if (urlDestino.Length > 0
            && (!Uri.TryCreate(urlDestino, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            // A URL vira destino de um POST feito pelo servidor: esquema exótico aqui é
            // superfície de SSRF, não conveniência.
            Erro = "A URL de destino precisa ser http(s) absoluta.";
            return RedirectToPage(new { id });
        }

        if (ativo && urlDestino.Length == 0)
        {
            Erro = "Não dá para ligar o roteamento sem destino — os eventos cairiam sem rota.";
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
        await db.SaveChangesAsync(ct);
        Recado = "Segredo removido. Voltamos a repassar a assinatura da Meta.";
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
                    Erro = $"O número {vindo.DisplayPhoneNumber ?? vindo.Id} já está cadastrado em outro WABA.";
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
        if (url.Length > 0
            && (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            Erro = "A URL precisa ser http(s) absoluta.";
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
