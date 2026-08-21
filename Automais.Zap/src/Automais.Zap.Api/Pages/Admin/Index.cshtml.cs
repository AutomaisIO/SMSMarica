using Automais.Zap.Api.Infra;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class IndexModel(ZapDbContext db, EscopoUsuario escopo, TimeProvider relogio) : PageModel
{
    public sealed record LinhaTenant(Tenant Tenant, int Wabas, int Numeros);

    public List<LinhaTenant> Tenants { get; private set; } = [];
    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var visiveis = await escopo.VisiveisAsync(ct);

        // Quem só enxerga um tenant não precisa de uma lista de um item.
        if (!Global && visiveis.Count == 1)
        {
            return Redirect($"/admin/tenant?id={visiveis[0].Id}");
        }

        var ids = visiveis.Select(t => t.Id).ToList();
        var contagens = await db.Wabas.AsNoTracking()
            .Where(w => ids.Contains(w.TenantId))
            .GroupBy(w => w.TenantId)
            .Select(g => new { TenantId = g.Key, Wabas = g.Count(), Numeros = g.Sum(w => w.Numeros.Count) })
            .ToListAsync(ct);

        Tenants = visiveis.Select(t =>
        {
            var c = contagens.FirstOrDefault(x => x.TenantId == t.Id);
            return new LinhaTenant(t, c?.Wabas ?? 0, c?.Numeros ?? 0);
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostCriarAsync(string nome, string? observacao, CancellationToken ct)
    {
        // Só quem é global cria tenant: criar tenant é ato comercial, não de operação.
        if (!escopo.Global) return Forbid();

        nome = (nome ?? "").Trim();
        if (nome.Length == 0) { Erro = "Informe o nome."; return RedirectToPage(); }
        if (await db.Tenants.AnyAsync(t => t.Nome == nome, ct))
        {
            Erro = $"Já existe um tenant chamado \"{nome}\".";
            return RedirectToPage();
        }

        var tenant = new Tenant
        {
            Nome = nome,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        return Redirect($"/admin/tenant?id={tenant.Id}");
    }

    public async Task<IActionResult> OnPostSelecionarAsync(Guid id, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();
        escopo.Selecionar(id);
        return Redirect($"/admin/tenant?id={id}");
    }
}
