using Automais.Zap.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Core.Roteamento;

/// <summary>
/// Consulta direta ao banco, sem cache. O banco é local ao droplet e a busca é por índice
/// único — cache aqui só traria bug de invalidação (mudou a rota na tela, o relay continua
/// entregando no destino velho) em troca de microssegundos.
/// </summary>
public sealed class Roteador(ZapDbContext db) : IRoteador
{
    public async Task<IReadOnlyDictionary<string, RotaDestino>> ResolverPorNumeroAsync(
        IReadOnlyCollection<string> phoneNumberIds, CancellationToken ct = default)
    {
        if (phoneNumberIds.Count == 0) return new Dictionary<string, RotaDestino>();

        // Quatro chaves precisam estar ligadas para um evento ser entregue: o número, o
        // roteamento do WABA, o tenant ativo e o tenant não suspenso. Qualquer uma desligada
        // derruba a entrega — é assim que a suspensão do canal funciona sem tocar na instância.
        //
        // O predicado fica inline de propósito: extraído para um método, o EF não traduz e a
        // consulta cai para avaliação em memória.
        var linhas = await db.Numeros
            .AsNoTracking()
            .Where(n => phoneNumberIds.Contains(n.PhoneNumberId)
                        && n.Ativo
                        && n.Waba!.RoteamentoAtivo
                        && n.Waba.Tenant!.Ativo
                        && n.Waba.Tenant.SuspensoEm == null
                        && ((n.UrlDestinoOverride != null && n.UrlDestinoOverride != "")
                            || (n.Waba.UrlDestino != null && n.Waba.UrlDestino != "")))
            .Select(n => new
            {
                n.PhoneNumberId,
                n.Waba!.TenantId,
                Nome = n.Waba.Tenant!.Nome,
                // Override do número ganha do destino do WABA; vazio herda.
                Url = n.UrlDestinoOverride != null && n.UrlDestinoOverride != ""
                    ? n.UrlDestinoOverride
                    : n.Waba.UrlDestino!,
            })
            .ToListAsync(ct);

        return linhas.ToDictionary(
            x => x.PhoneNumberId,
            x => new RotaDestino(x.TenantId, x.Nome, x.Url));
    }

    public async Task<IReadOnlyDictionary<string, RotaDestino>> ResolverPorWabaAsync(
        IReadOnlyCollection<string> wabaIds, CancellationToken ct = default)
    {
        if (wabaIds.Count == 0) return new Dictionary<string, RotaDestino>();

        var linhas = await db.Wabas
            .AsNoTracking()
            .Where(w => wabaIds.Contains(w.WabaId)
                        && w.RoteamentoAtivo
                        && w.UrlDestino != null && w.UrlDestino != ""
                        && w.Tenant!.Ativo && w.Tenant.SuspensoEm == null)
            .Select(w => new { w.WabaId, w.TenantId, Nome = w.Tenant!.Nome, Url = w.UrlDestino! })
            .ToListAsync(ct);

        return linhas.ToDictionary(
            x => x.WabaId,
            x => new RotaDestino(x.TenantId, x.Nome, x.Url));
    }
}
