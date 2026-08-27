using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

public sealed partial class TemplatesModel(ZapDbContext db, EscopoUsuario escopo, IGraphMetaClient graph) : PageModel
{
    /// <summary>As três categorias que a Cloud API aceita hoje.</summary>
    public static readonly string[] Categorias = ["UTILITY", "MARKETING", "AUTHENTICATION"];

    [GeneratedRegex(@"\{\{\d{1,3}\}\}")]
    private static partial Regex RegexVariavel();

    public Data.Entities.Waba? Alvo { get; private set; }
    public List<Data.Entities.Waba> WabasDoTenant { get; private set; } = [];
    public IReadOnlyList<TemplateMeta> Templates { get; private set; } = [];
    public string? ErroGraph { get; private set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    /// <summary>
    /// Sem id, cai no primeiro WABA do cliente selecionado — o link do menu não precisa
    /// saber de WABA nenhum.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        var selecionado = await escopo.SelecionadoAsync(ct);

        if (id is null)
        {
            if (selecionado is null) return Page();
            WabasDoTenant = await WabasDeAsync(selecionado.Id, ct);
            Alvo = WabasDoTenant.FirstOrDefault();
        }
        else
        {
            Alvo = await AutorizarAsync(id.Value, ct);
            if (Alvo is null) return Forbid();
            WabasDoTenant = await WabasDeAsync(Alvo.TenantId, ct);

            // Chegar por URL aos templates de outro tenant realinha o contexto do painel.
            escopo.Selecionar(Alvo.TenantId);
        }

        if (Alvo is null) return Page();

        var r = await graph.ListarTemplatesAsync(Alvo.WabaId, ct);
        if (r.Sucesso) Templates = r.Valor!;
        else ErroGraph = r.Erro;

        return Page();
    }

    /// <summary>Corpo do template com as variáveis {{n}} destacadas, já escapado.</summary>
    public IHtmlContent CorpoMarcado(string? corpo)
    {
        if (string.IsNullOrEmpty(corpo)) return new HtmlString("—");

        var html = new HtmlContentBuilder();
        var pos = 0;
        foreach (Match m in RegexVariavel().Matches(corpo))
        {
            html.Append(corpo[pos..m.Index]);
            html.AppendHtml("<mark>");
            html.Append(m.Value);
            html.AppendHtml("</mark>");
            pos = m.Index + m.Length;
        }
        html.Append(corpo[pos..]);

        using var sw = new StringWriter();
        html.WriteTo(sw, HtmlEncoder.Default);
        return new HtmlString(sw.ToString());
    }

    private async Task<List<Data.Entities.Waba>> WabasDeAsync(Guid tenantId, CancellationToken ct)
        => await db.Wabas.AsNoTracking()
            .Where(w => w.TenantId == tenantId)
            .OrderBy(w => w.Nome ?? w.WabaId)
            .ToListAsync(ct);

    private async Task<Data.Entities.Waba?> AutorizarAsync(Guid id, CancellationToken ct)
    {
        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (waba is null) return null;
        return await escopo.PodeVerAsync(waba.TenantId, ct) ? waba : null;
    }

    public async Task<IActionResult> OnPostCriarAsync(
        Guid id, string nome, string idioma, string categoria, string corpo,
        string? cabecalho, string? rodape, string? exemplos, CancellationToken ct)
    {
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

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
        var waba = await AutorizarAsync(id, ct);
        if (waba is null) return Forbid();

        var r = await graph.ExcluirTemplateAsync(waba.WabaId, nome, ct);
        if (r.Sucesso) Recado = $"Template \"{nome}\" excluído.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }
}
