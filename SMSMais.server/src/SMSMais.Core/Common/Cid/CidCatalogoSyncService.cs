using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMais.Core.Common.Cid;

public sealed record CidCatalogoSyncResultadoDto(int Total, int Inseridos, int Atualizados);

/// <summary>
/// Consolida os espelhos de CID do SER e do SERNIT na NOSSA tabela canônica <see cref="Cid"/>
/// (<c>smsmarica.cid</c>). A partir daí a descrição do "diagnóstico inicial" (ticket #155) é lida
/// só da nossa base — nem dos espelhos, nem ao vivo dos sistemas de regulação.
///
/// <para>Roda sob demanda (endpoint admin) e automaticamente ao fim da importação de cada catálogo
/// SER/SERNIT. Idempotente e aditivo: faz upsert por código e <b>não apaga</b> códigos que sumirem
/// de uma origem — a descrição de ontem ainda é melhor que nenhuma.</para>
/// </summary>
public interface ICidCatalogoSyncService
{
    Task<CidCatalogoSyncResultadoDto> SincronizarAsync(CancellationToken ct = default);
}

public sealed class CidCatalogoSyncService(SmsMaisDbContext db) : ICidCatalogoSyncService
{
    public async Task<CidCatalogoSyncResultadoDto> SincronizarAsync(CancellationToken ct = default)
    {
        // SER é a fonte preferida; o SERNIT completa o que falta (os CID de rastreamento do
        // capítulo Z, que o SER não traz). Descrição estável por código nos dois.
        var desejado = new Dictionary<string, (string Descricao, string Fonte)>(StringComparer.Ordinal);

        foreach (var x in await db.SerCatalogoCids.AsNoTracking()
                     .Select(c => new { c.Codigo, c.Descricao }).ToListAsync(ct))
            if (Normalizar(x.Codigo) is { } cod && !string.IsNullOrWhiteSpace(x.Descricao))
                desejado.TryAdd(cod, (x.Descricao.Trim(), "SER"));

        foreach (var x in await db.SernitCatalogoCids.AsNoTracking()
                     .Select(c => new { c.Codigo, c.Descricao }).ToListAsync(ct))
            if (Normalizar(x.Codigo) is { } cod && !string.IsNullOrWhiteSpace(x.Descricao)
                && !desejado.ContainsKey(cod))
                desejado[cod] = (x.Descricao.Trim(), "SERNIT");

        var existentes = await db.Cids.ToDictionaryAsync(c => c.Codigo, ct);
        var agora = DateTime.UtcNow;
        int inseridos = 0, atualizados = 0;

        foreach (var (codigo, (descricao, fonte)) in desejado)
        {
            if (existentes.TryGetValue(codigo, out var atual))
            {
                if (atual.Descricao != descricao || atual.Fonte != fonte)
                {
                    atual.Descricao = descricao;
                    atual.Fonte = fonte;
                    atual.AtualizadoEm = agora;
                    atualizados++;
                }
            }
            else
            {
                db.Cids.Add(new SMSMais.Data.Entities.Cid
                {
                    Codigo = codigo,
                    Descricao = descricao,
                    Fonte = fonte,
                    CriadoEm = agora,
                });
                inseridos++;
            }
        }

        if (inseridos > 0 || atualizados > 0) await db.SaveChangesAsync(ct);
        return new CidCatalogoSyncResultadoDto(desejado.Count, inseridos, atualizados);
    }

    private static string? Normalizar(string? codigo)
    {
        var t = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        return t.Length == 0 ? null : t;
    }
}
