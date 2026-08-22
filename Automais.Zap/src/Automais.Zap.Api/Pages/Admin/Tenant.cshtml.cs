using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Admin;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Tokens;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class TenantModel(
    ZapDbContext db,
    EscopoUsuario escopo,
    IAdminService admin,
    IGraphMetaClient graph,
    ITokenService tokens,
    TimeProvider relogio) : PageModel
{
    public Data.Entities.Tenant? Alvo { get; private set; }
    public List<Data.Entities.Waba> Wabas { get; private set; } = [];
    public IReadOnlyList<UsuarioListado> Usuarios { get; private set; } = [];
    public IReadOnlyList<Data.Entities.TenantToken> Tokens { get; private set; } = [];
    public List<Data.Entities.Numero> NumerosDoTenant { get; private set; } = [];
    public int FalhasUltimas24h { get; private set; }

    /// <summary>Token em claro, exibido UMA vez logo apos criar. Nao ha como reexibir.</summary>
    [TempData] public string? TokenNovo { get; set; }
    public bool Global => escopo.Global;

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();
        await CarregarAsync(id, ct);
        if (Alvo is null) return NotFound();

        escopo.Selecionar(id);
        return Page();
    }

    private async Task CarregarAsync(Guid id, CancellationToken ct)
    {
        Alvo = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (Alvo is null) return;

        Wabas = await db.Wabas.AsNoTracking()
            .Include(w => w.Numeros)
            .Where(w => w.TenantId == id)
            .OrderBy(w => w.Nome ?? w.WabaId)
            .ToListAsync(ct);

        Usuarios = await admin.ListarUsuariosAsync(id, ct);
        Tokens = await tokens.ListarAsync(id, ct);

        NumerosDoTenant = await db.Numeros.AsNoTracking()
            .Where(n => n.Waba!.TenantId == id)
            .OrderBy(n => n.DisplayPhoneNumber)
            .ToListAsync(ct);

        var corte = relogio.GetUtcNow().AddHours(-24);
        FalhasUltimas24h = await db.EntregasLog.AsNoTracking()
            .CountAsync(x => x.TenantId == id && !x.Sucesso && x.RecebidoEm >= corte, ct);
    }

    public async Task<IActionResult> OnPostCriarTokenAsync(
        Guid id, string nome, string alcance, Guid[]? numeros, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        nome = (nome ?? "").Trim();
        if (nome.Length == 0)
        {
            Erro = "Dê um nome ao token — é como você vai saber qual revogar depois.";
            return RedirectToPage(new { id });
        }

        var todos = alcance != "selecionados";
        var escolhidos = numeros ?? [];
        if (!todos && escolhidos.Length == 0)
        {
            Erro = "Escolha ao menos um número, ou marque que o token vale para todos.";
            return RedirectToPage(new { id });
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
                Erro = "Nenhum dos números escolhidos pertence a este tenant.";
                return RedirectToPage(new { id });
            }
        }

        var criado = await tokens.CriarAsync(id, nome, todos, escolhidos, ct);
        TokenNovo = criado.ValorEmClaro;
        Recado = "Token criado. Copie agora — ele não é exibido de novo.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRevogarTokenAsync(Guid id, Guid tokenId, CancellationToken ct)
    {
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        var alvo = await db.TenantTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tokenId, ct);
        if (alvo is null || alvo.TenantId != id) return Forbid();

        await tokens.RevogarAsync(tokenId, ct);
        Recado = "Token revogado. Quem estiver usando para de conseguir enviar agora.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSuspenderAsync(Guid id, string? motivo, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound();

        if (tenant.SuspensoEm is null)
        {
            tenant.SuspensoEm = relogio.GetUtcNow();
            tenant.SuspensoMotivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            Recado = "Canal suspenso. Os WABAs deste tenant param de receber agora.";
        }
        else
        {
            tenant.SuspensoEm = null;
            tenant.SuspensoMotivo = null;
            Recado = "Canal religado.";
        }

        tenant.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAdicionarWabaAsync(Guid id, string wabaId, CancellationToken ct)
    {
        // Ato da casa: a validacao usa o token do System User da PLATAFORMA, que enxerga os WABAs
        // de todos os clientes — um operador de tenant poderia reivindicar o WABA de outro.
        if (!escopo.Global) return Forbid();
        if (!await escopo.PodeVerAsync(id, ct)) return Forbid();

        wabaId = (wabaId ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(wabaId, @"^\d{5,40}$"))
        {
            Erro = "Informe o ID numérico do WABA.";
            return RedirectToPage(new { id });
        }

        if (await db.Wabas.AnyAsync(w => w.WabaId == wabaId, ct))
        {
            Erro = "Esse WABA já está cadastrado — em algum tenant.";
            return RedirectToPage(new { id });
        }

        // Valida contra a Meta antes de gravar: id digitado errado viraria linha órfã que
        // depois ninguém sabe se é engano ou WABA que perdeu acesso.
        var r = await graph.ObterWabaAsync(wabaId, ct);
        if (!r.Sucesso)
        {
            Erro = "A Meta não reconheceu esse WABA: " + r.Erro;
            return RedirectToPage(new { id });
        }

        var waba = new Data.Entities.Waba
        {
            TenantId = id,
            WabaId = wabaId,
            Nome = r.Valor!.Nome,
            RoteamentoAtivo = false,
            CriadoEm = relogio.GetUtcNow(),
            SincronizadoEm = relogio.GetUtcNow(),
        };
        db.Wabas.Add(waba);
        await db.SaveChangesAsync(ct);

        return Redirect($"/admin/waba?id={waba.Id}");
    }

    public async Task<IActionResult> OnPostCriarUsuarioAsync(
        Guid id, string email, string nome, string senha, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        var (ok, erro) = await admin.CriarUsuarioAsync(email, nome, senha, id, ct);
        if (ok) Recado = $"Usuário {email} criado.";
        else Erro = erro;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDesvincularAsync(Guid id, Guid usuarioId, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();
        await admin.DesvincularAsync(usuarioId, id, ct);
        Recado = "Usuário desvinculado deste tenant.";
        return RedirectToPage(new { id });
    }
}
