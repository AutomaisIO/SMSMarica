using Automais.Zap.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Core.Roteamento;

/// <summary>
/// Consulta direta ao banco, sem cache. O banco é local ao droplet e a busca é um índice
/// único — cache aqui só traria bug de invalidação (mudou a rota na tela, o relay continua
/// entregando no destino velho) em troca de microssegundos.
/// </summary>
public sealed class Roteador(ZapDbContext db) : IRoteador
{
    public async Task<IReadOnlyDictionary<string, RotaDestino>> ResolverPorNumeroAsync(
        IReadOnlyCollection<string> phoneNumberIds, CancellationToken ct = default)
    {
        if (phoneNumberIds.Count == 0) return new Dictionary<string, RotaDestino>();

        var linhas = await db.Numeros
            .AsNoTracking()
            .Where(n => phoneNumberIds.Contains(n.PhoneNumberId) && n.Ativo && n.Destino!.Ativo)
            .Select(n => new { n.PhoneNumberId, n.DestinoId, n.Destino!.Nome, n.Destino.UrlWebhook })
            .ToListAsync(ct);

        return linhas.ToDictionary(
            x => x.PhoneNumberId,
            x => new RotaDestino(x.DestinoId, x.Nome, x.UrlWebhook));
    }

    public async Task<IReadOnlyDictionary<string, RotaDestino>> ResolverPorWabaAsync(
        IReadOnlyCollection<string> wabaIds, CancellationToken ct = default)
    {
        if (wabaIds.Count == 0) return new Dictionary<string, RotaDestino>();

        var linhas = await db.Numeros
            .AsNoTracking()
            .Where(n => n.WabaId != null && wabaIds.Contains(n.WabaId) && n.Ativo && n.Destino!.Ativo)
            .Select(n => new { Waba = n.WabaId!, n.DestinoId, n.Destino!.Nome, n.Destino.UrlWebhook })
            .ToListAsync(ct);

        var resultado = new Dictionary<string, RotaDestino>();
        foreach (var grupo in linhas.GroupBy(x => x.Waba))
        {
            var distintos = grupo.DistinctBy(x => x.DestinoId).ToList();
            if (distintos.Count != 1) continue; // ambíguo: não entrega
            var d = distintos[0];
            resultado[grupo.Key] = new RotaDestino(d.DestinoId, d.Nome, d.UrlWebhook);
        }

        return resultado;
    }
}
