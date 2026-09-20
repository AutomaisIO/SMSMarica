using System.Security.Claims;
using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// A conta de quem está logado: seus dados e a troca da própria senha.
///
/// <para>Existe porque não havia onde: a Equipe é a tela de quem administra os OUTROS, e quem
/// recebeu uma senha inicial da casa não tinha como deixar de usá-la. O /admin/perfil é o
/// perfil COMERCIAL do número no WhatsApp — outra coisa.</para>
///
/// <para>Sob o mesmo limitador da tela de login (10/min por IP): aqui também se adivinha
/// senha, com a diferença de que a atual já está pela metade.</para>
/// </summary>
[EnableRateLimiting("entrar")]
public sealed class ContaModel(EscopoUsuario escopo, IAdminService admin) : PageModel
{
    public Data.Entities.UsuarioAdmin? Usuario { get; private set; }

    /// <summary>Clientes que este operador enxerga — o global vê todos, e a lista diz quantos.</summary>
    public IReadOnlyList<string> Clientes { get; private set; } = [];

    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await CarregarAsync(ct);
        return Usuario is null ? RedirectToPage("/Admin/Sair") : Page();
    }

    public async Task<IActionResult> OnPostNomeAsync(string nome, CancellationToken ct)
    {
        var (ok, erro) = await admin.AlterarNomeAsync(escopo.UsuarioId, nome, ct);
        if (!ok)
        {
            Erro = erro;
            return RedirectToPage();
        }

        // O nome vive no cookie de autenticação: sem reemitir, o cabeçalho continuaria
        // mostrando o antigo até o próximo login.
        await ReemitirCookieAsync(nome.Trim(), ct);
        Recado = "Nome atualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSenhaAsync(
        string senhaAtual, string novaSenha, string confirmacao, CancellationToken ct)
    {
        if (novaSenha != confirmacao)
        {
            Erro = "A confirmação não bate com a senha nova.";
            return RedirectToPage();
        }

        var (ok, erro) = await admin.TrocarSenhaAsync(escopo.UsuarioId, senhaAtual, novaSenha, ct);
        Recado = ok ? "Senha trocada." : null;
        Erro = erro;
        return RedirectToPage();
    }

    private async Task CarregarAsync(CancellationToken ct)
    {
        Usuario = await admin.ObterAsync(escopo.UsuarioId, ct);
        if (Usuario is null) return;

        Clientes = Global
            ? [.. (await escopo.VisiveisAsync(ct)).Select(t => t.Nome).OrderBy(n => n)]
            : [.. Usuario.Tenants.Select(t => t.Tenant?.Nome ?? "—").OrderBy(n => n)];
    }

    private async Task ReemitirCookieAsync(string nome, CancellationToken ct)
    {
        var atual = await admin.ObterAsync(escopo.UsuarioId, ct);
        if (atual is null) return;

        var identidade = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, atual.Id.ToString()),
                new Claim(ClaimTypes.Name, nome),
                new Claim(ClaimTypes.Email, atual.Email),
                new Claim(EscopoUsuario.ClaimGlobal, atual.Global ? "1" : "0"),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
    }
}
