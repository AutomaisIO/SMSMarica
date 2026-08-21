using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class WabasModel(
    ZapDbContext db,
    IGraphMetaClient graph,
    IConfiguracaoMetaService configuracao,
    TimeProvider relogio) : PageModel
{
    public List<Waba> Wabas { get; private set; } = [];
    public List<Destino> Destinos { get; private set; } = [];
    public bool PodeConsultarMeta { get; private set; }

    /// <summary>WABA aberto no detalhe, quando <c>?id=</c> vem na URL.</summary>
    public Waba? Aberto { get; private set; }
    public string? AppIdProprio { get; private set; }
    public IReadOnlyList<NumeroMeta> Numeros { get; private set; } = [];
    public IReadOnlyList<AppInscrito> AppsInscritos { get; private set; } = [];
    public HashSet<string> NumerosJaRoteados { get; private set; } = [];
    public string? ErroGraph { get; private set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public bool EsteAppInscrito => AppIdProprio is not null && AppsInscritos.Any(a => a.Id == AppIdProprio);

    public async Task OnGetAsync(Guid? id, CancellationToken ct) => await CarregarAsync(id, ct);

    private async Task CarregarAsync(Guid? id, CancellationToken ct)
    {
        Wabas = await db.Wabas.AsNoTracking().OrderBy(w => w.Nome ?? w.WabaId).ToListAsync(ct);
        Destinos = await db.Destinos.AsNoTracking().OrderBy(d => d.Nome).ToListAsync(ct);

        var creds = await configuracao.ObterAsync(ct);
        PodeConsultarMeta = creds.PodeGerenciar;
        AppIdProprio = creds.AppId;

        if (id is null) return;

        Aberto = Wabas.FirstOrDefault(w => w.Id == id);
        if (Aberto is null || !PodeConsultarMeta) return;

        var numeros = await graph.ListarNumerosAsync(Aberto.WabaId, ct);
        if (numeros.Sucesso) Numeros = numeros.Valor!;
        else ErroGraph = numeros.Erro;

        var apps = await graph.ListarAppsInscritosAsync(Aberto.WabaId, ct);
        if (apps.Sucesso) AppsInscritos = apps.Valor!;
        else ErroGraph ??= apps.Erro;

        NumerosJaRoteados = (await db.Numeros.AsNoTracking()
            .Select(n => n.PhoneNumberId).ToListAsync(ct)).ToHashSet();
    }

    public async Task<IActionResult> OnPostAdicionarAsync(string wabaId, CancellationToken ct)
    {
        wabaId = (wabaId ?? "").Trim();
        if (wabaId.Length == 0) { Erro = "Informe o ID do WABA."; return RedirectToPage(); }

        if (await db.Wabas.AnyAsync(w => w.WabaId == wabaId, ct))
        {
            Erro = "Esse WABA já está cadastrado.";
            return RedirectToPage();
        }

        // Consulta a Meta antes de gravar: um id digitado errado vira uma linha inútil que
        // depois ninguém sabe se é engano ou WABA que perdeu acesso.
        var r = await graph.ObterWabaAsync(wabaId, ct);
        if (!r.Sucesso)
        {
            Erro = "A Meta não reconheceu esse WABA: " + r.Erro;
            return RedirectToPage();
        }

        db.Wabas.Add(new Waba
        {
            WabaId = wabaId,
            Nome = r.Valor!.Nome,
            CriadoEm = relogio.GetUtcNow(),
            SincronizadoEm = relogio.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        Recado = $"WABA \"{r.Valor!.Nome}\" cadastrado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoverAsync(Guid id, CancellationToken ct)
    {
        var waba = await db.Wabas.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return RedirectToPage();

        db.Wabas.Remove(waba);
        await db.SaveChangesAsync(ct);
        Recado = "WABA removido do console. As rotas dos números dele continuam intactas.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostInscreverAsync(Guid id, CancellationToken ct)
    {
        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return RedirectToPage();

        var r = await graph.InscreverAppNoWabaAsync(waba.WabaId, ct);
        if (r.Sucesso) Recado = "App inscrito. A Meta passa a entregar os eventos deste WABA no nosso webhook.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDesinscreverAsync(Guid id, CancellationToken ct)
    {
        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return RedirectToPage();

        var r = await graph.DesinscreverAppDoWabaAsync(waba.WabaId, ct);
        if (r.Sucesso) Recado = "App desinscrito. Este WABA para de entregar eventos aqui.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostImportarNumeroAsync(
        Guid id, string phoneNumberId, string? displayPhoneNumber, string? rotulo, Guid destinoId, CancellationToken ct)
    {
        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return RedirectToPage();

        if (await db.Numeros.AnyAsync(n => n.PhoneNumberId == phoneNumberId, ct))
        {
            Erro = $"O número {phoneNumberId} já está roteado.";
            return RedirectToPage(new { id });
        }

        if (!await db.Destinos.AnyAsync(d => d.Id == destinoId, ct))
        {
            Erro = "Escolha um destino.";
            return RedirectToPage(new { id });
        }

        db.Numeros.Add(new Numero
        {
            PhoneNumberId = phoneNumberId,
            DestinoId = destinoId,
            WabaId = waba.WabaId,
            DisplayPhoneNumber = displayPhoneNumber,
            Rotulo = string.IsNullOrWhiteSpace(rotulo) ? null : rotulo.Trim(),
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        Recado = $"Número {displayPhoneNumber ?? phoneNumberId} roteado.";
        return RedirectToPage(new { id });
    }
}
