using Automais.Zap.Api.Infra;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin.Plataforma;

public sealed class NovoModel(ZapDbContext db, EscopoUsuario escopo, TimeProvider relogio) : PageModel
{
    [TempData] public string? Erro { get; set; }

    public IActionResult OnGet()
        // Criar tenant é ato comercial, não de operação: só quem é da casa.
        => escopo.Global ? Page() : Forbid();

    public async Task<IActionResult> OnPostAsync(string nome, string? observacao, CancellationToken ct)
    {
        if (!escopo.Global) return Forbid();

        nome = (nome ?? "").Trim();
        if (nome.Length == 0)
        {
            Erro = "Informe o nome.";
            return RedirectToPage();
        }

        if (await db.Tenants.AnyAsync(t => t.Nome == nome, ct))
        {
            Erro = $"Já existe um tenant chamado \"{nome}\".";
            return RedirectToPage();
        }

        var tenant = new Tenant
        {
            Nome = nome,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        // Entra no tenant recém-criado: quem acabou de cadastrar quer configurar em seguida.
        escopo.Selecionar(tenant.Id);
        return Redirect($"/admin/tenant?id={tenant.Id}");
    }
}
