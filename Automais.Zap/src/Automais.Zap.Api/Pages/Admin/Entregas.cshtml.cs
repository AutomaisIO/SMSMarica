using Automais.Zap.Api.Infra;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class EntregasModel(ZapDbContext db, EscopoUsuario escopo) : PageModel
{
    public sealed record Linha(
        DateTimeOffset RecebidoEm,
        string? Tenant,
        string PhoneNumberId,
        string Tipo,
        bool Sucesso,
        int? StatusHttp,
        int DuracaoMs,
        string? Erro);

    public List<Linha> Linhas { get; private set; } = [];
    public int FalhasRecentes { get; private set; }
    public int SemRotaRecentes { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Quem nao e global so ve a trilha dos tenants dele. Sem esse recorte, a tela de
        // entregas vazaria o volume e os numeros de um municipio para outro.
        var permitidos = escopo.Global
            ? null
            : (await escopo.VisiveisAsync(ct)).Select(t => t.Id).ToList();

        Linhas = await db.EntregasLog
            .AsNoTracking()
            .Where(x => permitidos == null || (x.TenantId != null && permitidos.Contains(x.TenantId.Value)))
            .OrderByDescending(x => x.Id)
            .Take(200)
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

        FalhasRecentes = Linhas.Count(l => !l.Sucesso && l.Erro != "sem rota cadastrada");
        SemRotaRecentes = Linhas.Count(l => l.Erro == "sem rota cadastrada");
    }
}
