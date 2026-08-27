using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Tokens;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// Tokens de envio do cliente selecionado. A tela lê o tenant do seletor; os POSTs carregam
/// o id explícito e re-checam o escopo — trocar de cliente em outra aba não muda o alvo de
/// um formulário já aberto.
/// </summary>
public sealed class TokensModel(
    ZapDbContext db,
    EscopoUsuario escopo,
    ITokenService tokens) : PageModel
{
    public Data.Entities.Tenant? Alvo { get; private set; }
    public IReadOnlyList<Data.Entities.TenantToken> Tokens { get; private set; } = [];
    public List<Data.Entities.Numero> NumerosDoTenant { get; private set; } = [];

    /// <summary>Token em claro, exibido UMA vez logo apos criar. Nao ha como reexibir.</summary>
    [TempData] public string? TokenNovo { get; set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Alvo = await escopo.SelecionadoAsync(ct);
        if (Alvo is null) return Page();

        Tokens = await tokens.ListarAsync(Alvo.Id, ct);
        NumerosDoTenant = await db.Numeros.AsNoTracking()
            .Where(n => n.Waba!.TenantId == Alvo.Id)
            .OrderBy(n => n.DisplayPhoneNumber)
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnPostCriarAsync(
        Guid id, string nome, string alcance, Guid[]? numeros, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        nome = (nome ?? "").Trim();
        if (nome.Length == 0)
        {
            Erro = "Dê um nome ao token — é como você vai saber qual revogar depois.";
            return RedirectToPage();
        }

        var todos = alcance != "selecionados";
        var escolhidos = numeros ?? [];
        if (!todos && escolhidos.Length == 0)
        {
            Erro = "Escolha ao menos um número, ou marque que o token vale para todos.";
            return RedirectToPage();
        }

        // Números de OUTRO tenant não entram nem por request forjado.
        if (!todos)
        {
            var validos = await db.Numeros
                .Where(n => escolhidos.Contains(n.Id) && n.Waba!.TenantId == id)
                .Select(n => n.Id)
                .ToListAsync(ct);
            escolhidos = [.. validos];
            if (escolhidos.Length == 0)
            {
                Erro = "Nenhum dos números escolhidos pertence a este cliente.";
                return RedirectToPage();
            }
        }

        var criado = await tokens.CriarAsync(id, nome, todos, escolhidos, ct);
        TokenNovo = criado.ValorEmClaro;
        Recado = "Token criado. Copie agora — ele não é exibido de novo.";

        // O token nasceu para o tenant DO FORMULÁRIO; alinhar a seleção garante que a tela
        // seguinte mostra o token sob o cabeçalho certo, mesmo se outra aba trocou o cookie.
        escopo.Selecionar(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevogarAsync(Guid id, Guid tokenId, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        var alvo = await db.TenantTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tokenId, ct);
        if (alvo is null || alvo.TenantId != id) return Forbid();

        await tokens.RevogarAsync(tokenId, ct);
        Recado = "Token revogado. Quem estiver usando para de conseguir enviar agora.";
        escopo.Selecionar(id);
        return RedirectToPage();
    }
}
