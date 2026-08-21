using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed class TemplatesModel(ZapDbContext db, IGraphMetaClient graph) : PageModel
{
    /// <summary>As três categorias que a Cloud API aceita hoje.</summary>
    public static readonly string[] Categorias = ["UTILITY", "MARKETING", "AUTHENTICATION"];

    public List<Waba> Wabas { get; private set; } = [];
    public Waba? Aberto { get; private set; }
    public IReadOnlyList<TemplateMeta> Templates { get; private set; } = [];
    public string? ErroGraph { get; private set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        Wabas = await db.Wabas.AsNoTracking().OrderBy(w => w.Nome ?? w.WabaId).ToListAsync(ct);
        Aberto = id is null ? Wabas.FirstOrDefault() : Wabas.FirstOrDefault(w => w.Id == id);
        if (Aberto is null) return;

        var r = await graph.ListarTemplatesAsync(Aberto.WabaId, ct);
        if (r.Sucesso) Templates = r.Valor!;
        else ErroGraph = r.Erro;
    }

    public async Task<IActionResult> OnPostCriarAsync(
        Guid id, string nome, string idioma, string categoria, string corpo,
        string? cabecalho, string? rodape, string? exemplos, CancellationToken ct)
    {
        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return RedirectToPage();

        // A Meta só aceita nome em minúsculas com underscore; corrigir aqui evita uma ida à
        // API para receber um erro que a gente já sabe prever.
        nome = (nome ?? "").Trim().ToLowerInvariant().Replace(' ', '_');

        var lista = (exemplos ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var r = await graph.CriarTemplateAsync(waba.WabaId,
            new NovoTemplate(nome, idioma, categoria, corpo, cabecalho, rodape, lista), ct);

        if (r.Sucesso)
        {
            Recado = $"Template \"{nome}\" enviado para aprovação (id {r.Valor}). " +
                     "O resultado chega pelo webhook, em message_template_status_update.";
        }
        else
        {
            Erro = "A Meta recusou: " + r.Erro;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostExcluirAsync(Guid id, string nome, CancellationToken ct)
    {
        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return RedirectToPage();

        var r = await graph.ExcluirTemplateAsync(waba.WabaId, nome, ct);
        if (r.Sucesso) Recado = $"Template \"{nome}\" excluído.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }
}
