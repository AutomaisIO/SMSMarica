using System.Security.Claims;
using Automais.Zap.Core.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class EntrarModel(IAdminService admin, ILogger<EntrarModel> logger) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Senha { get; set; } = string.Empty;

    public string? Erro { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/admin");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var usuario = await admin.AutenticarAsync(Email, Senha, ct);
        if (usuario is null)
        {
            // Mensagem única de propósito: dizer qual dos dois errou entrega enumeração de contas.
            logger.LogWarning("Tentativa de login recusada para {Email}.", Email);
            Erro = "E-mail ou senha inválidos.";
            return Page();
        }

        var identidade = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidade));

        return Redirect("/admin");
    }
}
