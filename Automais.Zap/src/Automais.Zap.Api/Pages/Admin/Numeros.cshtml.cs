using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// Todos os números do cliente selecionado, enriquecidos com o que a Meta responde por WABA
/// (uma chamada por WABA, não por número). Se a Graph API falhar, a tela degrada para o que
/// está no banco — mostrar menos é melhor que não mostrar nada.
/// </summary>
public sealed class NumerosModel(ZapDbContext db, EscopoUsuario escopo, IGraphMetaClient graph) : PageModel
{
    public sealed record LinhaNumero(
        Guid Id,
        string PhoneNumberId,
        string? Display,
        string? Rotulo,
        string? NomeVerificado,
        string? Qualidade,
        string? WabaNome,
        string WabaIdMeta,
        bool Ativo);

    public Data.Entities.Tenant? Alvo { get; private set; }
    public List<LinhaNumero> Linhas { get; private set; } = [];
    public string? ErroGraph { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return Page();

        var wabas = await db.Wabas.AsNoTracking()
            .Include(w => w.Numeros)
            .Where(w => w.TenantId == Alvo.Id)
            .OrderBy(w => w.Nome ?? w.WabaId)
            .ToListAsync(ct);

        foreach (var waba in wabas)
        {
            IReadOnlyList<NumeroMeta> naMeta = [];
            var r = await graph.ListarNumerosAsync(waba.WabaId, ct);
            if (r.Sucesso) naMeta = r.Valor!;
            else ErroGraph ??= r.Erro;

            foreach (var n in waba.Numeros.OrderBy(n => n.DisplayPhoneNumber))
            {
                var meta = naMeta.FirstOrDefault(m => m.Id == n.PhoneNumberId);
                Linhas.Add(new LinhaNumero(
                    n.Id,
                    n.PhoneNumberId,
                    n.DisplayPhoneNumber,
                    n.Rotulo,
                    meta?.VerifiedName,
                    meta?.QualityRating?.ToUpperInvariant() is "UNKNOWN" ? null : meta?.QualityRating?.ToUpperInvariant(),
                    waba.Nome,
                    waba.WabaId,
                    n.Ativo));
            }
        }

        return Page();
    }
}
