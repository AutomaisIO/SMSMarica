using Automais.Zap.Api.Infra;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin.Plataforma;

/// <summary>
/// Visão da casa: todos os tenants com a saúde de cada um. A pasta Plataforma já exige a
/// política Global — página nova aqui nasce protegida.
/// </summary>
public sealed class ClientesModel(ZapDbContext db, EscopoUsuario escopo, TimeProvider relogio) : PageModel
{
    public sealed record LinhaCliente(
        Guid Id,
        string Nome,
        string? Observacao,
        DateTimeOffset? SuspensoEm,
        string? SuspensoMotivo,
        int Wabas,
        int Numeros,
        int NumerosAtivos,
        int TokensAtivos,
        int Usuarios,
        int Entregues24h,
        int Falhas24h);

    public List<LinhaCliente> Linhas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var corte = relogio.GetUtcNow().AddHours(-24);

        // Uma consulta agregada por assunto, projetada no servidor — sem carregar coleções.
        Linhas = await db.Tenants.AsNoTracking()
            .OrderBy(t => t.Nome)
            .Select(t => new LinhaCliente(
                t.Id,
                t.Nome,
                t.Observacao,
                t.SuspensoEm,
                t.SuspensoMotivo,
                t.Wabas.Count,
                t.Wabas.SelectMany(w => w.Numeros).Count(),
                t.Wabas.SelectMany(w => w.Numeros).Count(n => n.Ativo),
                db.TenantTokens.Count(k => k.TenantId == t.Id && k.RevogadoEm == null),
                db.UsuariosTenant.Count(u => u.TenantId == t.Id),
                db.EntregasLog.Count(e => e.TenantId == t.Id && e.RecebidoEm >= corte && e.Sucesso),
                db.EntregasLog.Count(e => e.TenantId == t.Id && e.RecebidoEm >= corte && !e.Sucesso)))
            .ToListAsync(ct);
    }

    /// <summary>Abrir = selecionar o tenant e cair no painel dele, como o cliente vê.</summary>
    public async Task<IActionResult> OnPostAbrirAsync(Guid id, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();
        escopo.Selecionar(id);
        return Redirect($"/admin/tenant?id={id}");
    }
}
