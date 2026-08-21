using Automais.Zap.Api.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// A raiz do painel não é uma lista de tenants — é o tenant selecionado. Quem alterna no
/// seletor do cabeçalho passa a ver o painel como o cliente vê.
///
/// Esta página é só o despachante: manda para o tenant em foco e trata o caso de não haver
/// nenhum.
/// </summary>
public sealed class IndexModel(EscopoUsuario escopo) : PageModel
{
    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var selecionado = await escopo.SelecionadoAsync(ct);
        if (selecionado is not null) return Redirect($"/admin/tenant?id={selecionado.Id}");

        // Sem seleção válida no cookie: entra no primeiro visível em vez de exigir uma escolha
        // que o seletor do cabeçalho já permite fazer depois.
        var visiveis = await escopo.VisiveisAsync(ct);
        if (visiveis.Count > 0)
        {
            escopo.Selecionar(visiveis[0].Id);
            return Redirect($"/admin/tenant?id={visiveis[0].Id}");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSelecionarAsync(Guid id, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();
        escopo.Selecionar(id);
        return Redirect($"/admin/tenant?id={id}");
    }
}
