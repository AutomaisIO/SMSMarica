using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMais.Core.Common.Cid;

/// <summary>
/// Descrição por extenso de um código CID-10 a partir do catálogo canônico já importado
/// (<c>ser_catalogo_cid</c>: ~14k códigos, descrição estável por código — zero divergências medidas
/// em 06/10/2026). Serve para mostrar o "diagnóstico inicial" do pedido (o CID que o SISREG traz)
/// legível na tela da solicitação e da anamnese (ticket #155).
///
/// <para><b>Best-effort:</b> código fora do catálogo devolve <c>null</c> e a tela mostra só o código —
/// perder a descrição nunca pode esconder o CID.</para>
/// </summary>
public interface ICidCatalogoService
{
    /// <summary>Descrição de um código, ou <c>null</c> se vazio/desconhecido.</summary>
    Task<string?> DescricaoAsync(string? codigo, CancellationToken ct = default);

    /// <summary>Descrições de vários códigos de uma vez (uma query), por código normalizado.</summary>
    Task<IReadOnlyDictionary<string, string>> DescricoesAsync(
        IEnumerable<string?> codigos, CancellationToken ct = default);
}

public sealed class CidCatalogoService(SmsMaisDbContext db) : ICidCatalogoService
{
    public async Task<string?> DescricaoAsync(string? codigo, CancellationToken ct = default)
    {
        var c = Normalizar(codigo);
        if (c is null) return null;
        return await db.SerCatalogoCids.AsNoTracking()
            .Where(x => x.Codigo == c)
            .Select(x => x.Descricao)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> DescricoesAsync(
        IEnumerable<string?> codigos, CancellationToken ct = default)
    {
        var distintos = codigos.Select(Normalizar).OfType<string>().Distinct().ToList();
        if (distintos.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);

        var pares = await db.SerCatalogoCids.AsNoTracking()
            .Where(x => distintos.Contains(x.Codigo))
            .Select(x => new { x.Codigo, x.Descricao })
            .ToListAsync(ct);

        // Descrição estável por código; GroupBy protege de qualquer código repetido entre listas.
        return pares
            .GroupBy(p => p.Codigo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Descricao, StringComparer.Ordinal);
    }

    /// <summary>Normaliza o código do jeito que o catálogo guarda (sem dot, caixa alta, sem espaços).</summary>
    private static string? Normalizar(string? codigo)
    {
        var t = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        return t.Length == 0 ? null : t;
    }
}
