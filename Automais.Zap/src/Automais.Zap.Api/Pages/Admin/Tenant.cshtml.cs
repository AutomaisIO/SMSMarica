using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Admin;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class TenantModel(
    ZapDbContext db,
    EscopoUsuario escopo,
    IAdminService admin,
    IGraphMetaClient graph,
    TimeProvider relogio) : PageModel
{
    public Data.Entities.Tenant? Alvo { get; private set; }
    public List<Data.Entities.Waba> Wabas { get; private set; } = [];
    public IReadOnlyList<UsuarioListado> Usuarios { get; private set; } = [];
    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();
        await CarregarAsync(id, ct);
        if (Alvo is null) return NotFound();

        escopo.Selecionar(id);
        return Page();
    }

    private async Task CarregarAsync(Guid id, CancellationToken ct)
    {
        Alvo = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (Alvo is null) return;

        Wabas = await db.Wabas.AsNoTracking()
            .Include(w => w.Numeros)
            .Where(w => w.TenantId == id)
            .OrderBy(w => w.Nome ?? w.WabaId)
            .ToListAsync(ct);

        Usuarios = await admin.ListarUsuariosAsync(id, ct);
    }

    public async Task<IActionResult> OnPostSuspenderAsync(Guid id, string? motivo, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound();

        if (tenant.SuspensoEm is null)
        {
            tenant.SuspensoEm = relogio.GetUtcNow();
            tenant.SuspensoMotivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            Recado = "Canal suspenso. Os WABAs deste tenant param de receber agora.";
        }
        else
        {
            tenant.SuspensoEm = null;
            tenant.SuspensoMotivo = null;
            Recado = "Canal religado.";
        }

        tenant.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAdicionarWabaAsync(Guid id, string wabaId, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        wabaId = (wabaId ?? "").Trim();
        if (wabaId.Length == 0) { Erro = "Informe o ID do WABA."; return RedirectToPage(new { id }); }

        if (await db.Wabas.AnyAsync(w => w.WabaId == wabaId, ct))
        {
            Erro = "Esse WABA já está cadastrado — em algum tenant.";
            return RedirectToPage(new { id });
        }

        // Valida contra a Meta antes de gravar: id digitado errado viraria linha órfã que
        // depois ninguém sabe se é engano ou WABA que perdeu acesso.
        var r = await graph.ObterWabaAsync(wabaId, ct);
        if (!r.Sucesso)
        {
            Erro = "A Meta não reconheceu esse WABA: " + r.Erro;
            return RedirectToPage(new { id });
        }

        var waba = new Data.Entities.Waba
        {
            TenantId = id,
            WabaId = wabaId,
            Nome = r.Valor!.Nome,
            RoteamentoAtivo = false,
            CriadoEm = relogio.GetUtcNow(),
            SincronizadoEm = relogio.GetUtcNow(),
        };
        db.Wabas.Add(waba);
        await db.SaveChangesAsync(ct);

        return Redirect($"/admin/waba?id={waba.Id}");
    }

    public async Task<IActionResult> OnPostCriarUsuarioAsync(
        Guid id, string email, string nome, string senha, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var (ok, erro) = await admin.CriarUsuarioAsync(email, nome, senha, id, ct);
        if (ok) Recado = $"Usuário {email} criado.";
        else Erro = erro;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDesvincularAsync(Guid id, Guid usuarioId, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();
        await admin.DesvincularAsync(usuarioId, id, ct);
        Recado = "Usuário desvinculado deste tenant.";
        return RedirectToPage(new { id });
    }
}
