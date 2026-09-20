using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Core.Midias;

/// <summary>A arte escolhida para um modelo, com o que a tela precisa mostrar.</summary>
public sealed record ArteDoTemplate(
    string Template, Guid MidiaId, string NomeArquivo, string MimeType,
    long TamanhoBytes, int? Largura, int? Altura, DateTimeOffset AtualizadoEm)
{
    public string Caminho => $"/midias/{MidiaId}";
}

public interface ITemplateArteService
{
    /// <summary>Artes do WABA, por nome de modelo (comparação sem caso — a Meta já obriga minúsculas).</summary>
    Task<IReadOnlyDictionary<string, ArteDoTemplate>> MapaAsync(Guid wabaId, CancellationToken ct = default);

    /// <summary>
    /// Escolhe a arte de um modelo. A mídia precisa ser do MESMO tenant do WABA — sem isso, um
    /// id vazado apontaria a arte de um município no canal de outro.
    /// </summary>
    Task<(bool Ok, string? Erro)> DefinirAsync(
        Guid wabaId, string template, Guid midiaId, Guid? usuarioId, CancellationToken ct = default);

    Task<bool> RemoverAsync(Guid wabaId, string template, CancellationToken ct = default);

    /// <summary>Modelos que ainda usam esta mídia — o painel avisa antes de deixar apagar.</summary>
    Task<IReadOnlyList<string>> EmUsoPorAsync(Guid midiaId, CancellationToken ct = default);
}

public sealed class TemplateArteService(ZapDbContext db, TimeProvider relogio) : ITemplateArteService
{
    public async Task<IReadOnlyDictionary<string, ArteDoTemplate>> MapaAsync(
        Guid wabaId, CancellationToken ct = default)
    {
        var linhas = await db.TemplateArtes.AsNoTracking()
            .Where(a => a.WabaId == wabaId)
            .Select(a => new ArteDoTemplate(
                a.Template, a.MidiaId, a.Midia!.NomeArquivo, a.Midia.MimeType,
                a.Midia.TamanhoBytes, a.Midia.Largura, a.Midia.Altura, a.AtualizadoEm))
            .ToListAsync(ct);

        return linhas.ToDictionary(a => a.Template, a => a, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(bool Ok, string? Erro)> DefinirAsync(
        Guid wabaId, string template, Guid midiaId, Guid? usuarioId, CancellationToken ct = default)
    {
        var nome = (template ?? string.Empty).Trim();
        if (nome.Length == 0) return (false, "Informe o modelo.");

        var waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wabaId, ct);
        if (waba is null) return (false, "Conta WhatsApp não encontrada.");

        var midiaEhDoTenant = await db.Midias
            .AnyAsync(m => m.Id == midiaId && m.TenantId == waba.TenantId, ct);
        if (!midiaEhDoTenant) return (false, "Arte não encontrada neste cliente.");

        var atual = await db.TemplateArtes
            .FirstOrDefaultAsync(a => a.WabaId == wabaId && a.Template == nome, ct);
        if (atual is null)
        {
            atual = new TemplateArte { WabaId = wabaId, Template = nome };
            db.TemplateArtes.Add(atual);
        }

        atual.MidiaId = midiaId;
        atual.AtualizadoEm = relogio.GetUtcNow();
        atual.AtualizadoPorUsuarioId = usuarioId;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<bool> RemoverAsync(Guid wabaId, string template, CancellationToken ct = default)
    {
        var nome = (template ?? string.Empty).Trim();
        var apagadas = await db.TemplateArtes
            .Where(a => a.WabaId == wabaId && a.Template == nome)
            .ExecuteDeleteAsync(ct);
        return apagadas > 0;
    }

    public async Task<IReadOnlyList<string>> EmUsoPorAsync(Guid midiaId, CancellationToken ct = default)
        => await db.TemplateArtes.AsNoTracking()
            .Where(a => a.MidiaId == midiaId)
            .Select(a => a.Template)
            .OrderBy(t => t)
            .ToListAsync(ct);
}
