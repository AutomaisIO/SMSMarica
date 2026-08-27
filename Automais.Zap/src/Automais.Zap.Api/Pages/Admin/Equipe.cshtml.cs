using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>Usuários do cliente selecionado. Criar e desvincular são atos da casa.</summary>
public sealed class EquipeModel(EscopoUsuario escopo, IAdminService admin) : PageModel
{
    public Data.Entities.Tenant? Alvo { get; private set; }
    public IReadOnlyList<UsuarioListado> Usuarios { get; private set; } = [];

    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return Page();

        Usuarios = await admin.ListarUsuariosAsync(Alvo.Id, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCriarAsync(
        Guid id, string email, string nome, string senha, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var (ok, erro) = await admin.CriarUsuarioAsync(email, nome, senha, id, ct);
        if (ok) Recado = $"Usuário {email} criado.";
        else Erro = erro;

        // A ação valeu para o tenant do formulário — a tela seguinte mostra esse tenant.
        escopo.Selecionar(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDesvincularAsync(Guid id, Guid usuarioId, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();
        await admin.DesvincularAsync(usuarioId, id, ct);
        Recado = "Usuário desvinculado deste cliente.";
        escopo.Selecionar(id);
        return RedirectToPage();
    }
}
