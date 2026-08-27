using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// Painel do cliente: o retrato do canal. Tokens vivem em /admin/tokens e a equipe em
/// /admin/equipe — cada tela cuida de um assunto.
/// </summary>
public sealed class TenantModel(
    ZapDbContext db,
    EscopoUsuario escopo,
    IGraphMetaClient graph,
    TimeProvider relogio) : PageModel
{
    public Data.Entities.Tenant? Alvo { get; private set; }
    public List<Data.Entities.Waba> Wabas { get; private set; } = [];
    public int NumerosTotal { get; private set; }
    public int NumerosAtivos { get; private set; }
    public int TokensAtivos { get; private set; }
    public int FalhasUltimas24h { get; private set; }
    public int Entregues24h { get; private set; }

    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        Alvo = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (Alvo is null) return NotFound();

        Wabas = await db.Wabas.AsNoTracking()
            .Include(w => w.Numeros)
            .Where(w => w.TenantId == id)
            .OrderBy(w => w.Nome ?? w.WabaId)
            .ToListAsync(ct);

        NumerosTotal = Wabas.Sum(w => w.Numeros.Count);
        NumerosAtivos = Wabas.Sum(w => w.Numeros.Count(n => n.Ativo));

        TokensAtivos = await db.TenantTokens.AsNoTracking()
            .CountAsync(t => t.TenantId == id && t.RevogadoEm == null, ct);

        var corte = relogio.GetUtcNow().AddHours(-24);
        var trilha24h = db.EntregasLog.AsNoTracking().Where(x => x.TenantId == id && x.RecebidoEm >= corte);
        Entregues24h = await trilha24h.CountAsync(x => x.Sucesso, ct);
        FalhasUltimas24h = await trilha24h.CountAsync(x => !x.Sucesso, ct);

        escopo.Selecionar(id);
        return Page();
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
            Recado = "Canal suspenso. Os WABAs deste cliente param de receber agora.";
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
        // Ato da casa: a validacao usa o token do System User da PLATAFORMA, que enxerga os WABAs
        // de todos os clientes — um operador de tenant poderia reivindicar o WABA de outro.
        if (!escopo.Global) return Forbid();
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        wabaId = (wabaId ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(wabaId, @"^\d{5,40}$"))
        {
            Erro = "Informe o ID numérico do WABA.";
            return RedirectToPage(new { id });
        }

        if (await db.Wabas.AnyAsync(w => w.WabaId == wabaId, ct))
        {
            Erro = "Esse WABA já está cadastrado — em algum cliente.";
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
}
