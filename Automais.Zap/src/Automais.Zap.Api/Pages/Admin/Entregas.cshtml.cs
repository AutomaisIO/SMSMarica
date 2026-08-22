using Automais.Zap.Api.Infra;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class EntregasModel(ZapDbContext db, EscopoUsuario escopo, TimeProvider relogio) : PageModel
{
    public const int Pagina = 100;

    public sealed record Linha(
        DateTimeOffset RecebidoEm,
        string? Tenant,
        string PhoneNumberId,
        string Tipo,
        bool Sucesso,
        int? StatusHttp,
        int DuracaoMs,
        string? Erro);

    public sealed record OpcaoTenant(Guid Id, string Nome);

    public List<Linha> Linhas { get; private set; } = [];
    public List<OpcaoTenant> Tenants { get; private set; } = [];
    public int Total { get; private set; }
    public int Falhas24h { get; private set; }
    public int SemRota24h { get; private set; }
    public int Entregues24h { get; private set; }

    [BindProperty(SupportsGet = true)] public Guid? Tenant { get; set; }
    [BindProperty(SupportsGet = true)] public string? Tipo { get; set; }
    [BindProperty(SupportsGet = true)] public bool Falhas { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PaginaAtual { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Quem não é global só vê a trilha dos tenants dele. Sem esse recorte, a tela
        // vazaria o volume e os números de um município para outro.
        var visiveis = await escopo.VisiveisAsync(ct);
        Tenants = visiveis.Select(t => new OpcaoTenant(t.Id, t.Nome)).ToList();

        var permitidos = escopo.Global ? null : visiveis.Select(t => t.Id).ToList();
        if (Tenant is not null && !escopo.Global && !visiveis.Any(t => t.Id == Tenant))
        {
            // Filtro por tenant alheio não vira acesso.
            Tenant = null;
        }

        var consulta = db.EntregasLog.AsNoTracking()
            .Where(x => permitidos == null || (x.TenantId != null && permitidos.Contains(x.TenantId.Value)));

        if (Tenant is not null) consulta = consulta.Where(x => x.TenantId == Tenant);
        if (!string.IsNullOrWhiteSpace(Tipo)) consulta = consulta.Where(x => x.Tipo == Tipo);
        if (Falhas) consulta = consulta.Where(x => !x.Sucesso);

        Total = await consulta.CountAsync(ct);
        if (PaginaAtual < 1) PaginaAtual = 1;

        Linhas = await consulta
            .OrderByDescending(x => x.Id)
            .Skip((PaginaAtual - 1) * Pagina)
            .Take(Pagina)
            .Select(x => new Linha(
                x.RecebidoEm,
                x.TenantId == null ? null : db.Tenants.Where(t => t.Id == x.TenantId).Select(t => t.Nome).FirstOrDefault(),
                x.PhoneNumberId,
                x.Tipo,
                x.Sucesso,
                x.StatusHttp,
                x.DuracaoMs,
                x.Erro))
            .ToListAsync(ct);

        var corte = relogio.GetUtcNow().AddHours(-24);
        var base24 = db.EntregasLog.AsNoTracking()
            .Where(x => x.RecebidoEm >= corte)
            .Where(x => permitidos == null || (x.TenantId != null && permitidos.Contains(x.TenantId.Value)));
        if (Tenant is not null) base24 = base24.Where(x => x.TenantId == Tenant);

        Entregues24h = await base24.CountAsync(x => x.Sucesso, ct);
        SemRota24h = await base24.CountAsync(x => x.Erro == "sem rota cadastrada", ct);
        Falhas24h = await base24.CountAsync(x => !x.Sucesso && x.Erro != "sem rota cadastrada", ct);
    }

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Pagina));

    public string UrlPagina(int pagina) =>
        $"/admin/entregas?p={pagina}"
        + (Tenant is null ? "" : $"&tenant={Tenant}")
        + (string.IsNullOrWhiteSpace(Tipo) ? "" : $"&tipo={Uri.EscapeDataString(Tipo)}")
        + (Falhas ? "&falhas=true" : "");
}
