using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class EntregasModel(ZapDbContext db) : PageModel
{
    public sealed record Linha(
        DateTimeOffset RecebidoEm,
        string? Destino,
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
        Linhas = await db.EntregasLog
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(200)
            .Select(x => new Linha(
                x.RecebidoEm,
                x.DestinoId == null ? null : db.Destinos.Where(d => d.Id == x.DestinoId).Select(d => d.Nome).FirstOrDefault(),
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
