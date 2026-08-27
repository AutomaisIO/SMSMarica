using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// Perfil comercial do número — o que o cidadão vê no WhatsApp. Leitura e escrita direto na
/// Graph API; a plataforma não guarda cópia, então o que aparece aqui é o estado real.
/// </summary>
public sealed class PerfilModel(ZapDbContext db, EscopoUsuario escopo, IGraphMetaClient graph) : PageModel
{
    /// <summary>Limite da Meta para o upload da foto de perfil.</summary>
    private const long FotoMaxBytes = 5 * 1024 * 1024;

    public Data.Entities.Numero? Alvo { get; private set; }
    public Data.Entities.Waba? Waba { get; private set; }
    public PerfilNegocioMeta? Perfil { get; private set; }
    public string? ErroGraph { get; private set; }

    [TempData] public string? Recado { get; set; }
    [TempData] public string? Erro { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var permitido = await AutorizarAsync(id, ct);
        if (!permitido) return Forbid();
        if (Alvo is null) return NotFound();

        // Chegar por URL a um número de outro tenant realinha o contexto do painel.
        escopo.Selecionar(Waba!.TenantId);

        var r = await graph.ObterPerfilNegocioAsync(Alvo.PhoneNumberId, ct);
        if (r.Sucesso) Perfil = r.Valor;
        else ErroGraph = r.Erro;

        return Page();
    }

    public async Task<IActionResult> OnPostSalvarAsync(
        Guid id, string? sobre, string? descricao, string? endereco, string? email,
        string? site1, string? site2, string? vertical, CancellationToken ct)
    {
        var permitido = await AutorizarAsync(id, ct);
        if (!permitido) return Forbid();
        if (Alvo is null) return NotFound();

        var sites = new List<string>();
        foreach (var bruto in new[] { site1, site2 })
        {
            var site = (bruto ?? "").Trim();
            if (site.Length == 0) continue;
            if (!Uri.TryCreate(site, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                Erro = $"Site inválido: {site}. Use o endereço completo, com https://.";
                return RedirectToPage(new { id });
            }
            sites.Add(site);
        }

        var r = await graph.AtualizarPerfilNegocioAsync(Alvo.PhoneNumberId,
            new AtualizarPerfilNegocio(sobre, endereco, descricao, email, sites, vertical), ct);

        if (r.Sucesso) Recado = "Perfil salvo na Meta. O WhatsApp dos cidadãos passa a exibir os dados novos.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFotoAsync(Guid id, IFormFile? foto, CancellationToken ct)
    {
        var permitido = await AutorizarAsync(id, ct);
        if (!permitido) return Forbid();
        if (Alvo is null) return NotFound();

        if (foto is null || foto.Length == 0)
        {
            Erro = "Escolha o arquivo da foto antes de enviar.";
            return RedirectToPage(new { id });
        }

        if (foto.Length > FotoMaxBytes)
        {
            Erro = "A foto passa de 5 MB — reduza a imagem e tente de novo.";
            return RedirectToPage(new { id });
        }

        var tipo = (foto.ContentType ?? "").ToLowerInvariant();
        if (tipo is not ("image/jpeg" or "image/png"))
        {
            Erro = "A Meta só aceita JPEG ou PNG para a foto do perfil.";
            return RedirectToPage(new { id });
        }

        byte[] conteudo;
        await using (var ms = new MemoryStream())
        {
            await foto.CopyToAsync(ms, ct);
            conteudo = ms.ToArray();
        }

        var r = await graph.AtualizarFotoPerfilAsync(Alvo.PhoneNumberId, conteudo, tipo, ct);
        if (r.Sucesso) Recado = "Foto do perfil trocada na Meta.";
        else Erro = "A Meta recusou: " + r.Erro;

        return RedirectToPage(new { id });
    }

    private async Task<bool> AutorizarAsync(Guid id, CancellationToken ct)
    {
        Alvo = await db.Numeros.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        if (Alvo is null) return true;

        Waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == Alvo.WabaId, ct);
        if (Waba is null) { Alvo = null; return true; }

        // Fail-closed: número de tenant que o usuário não enxerga não existe.
        return await escopo.PodeVerAsync(Waba.TenantId, ct);
    }
}
