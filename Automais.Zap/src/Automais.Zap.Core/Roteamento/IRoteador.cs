namespace Automais.Zap.Core.Roteamento;

/// <summary>Destino resolvido para um número, já com a rota pronta.</summary>
public sealed record RotaDestino(Guid DestinoId, string Nome, string UrlWebhook);

public interface IRoteador
{
    /// <summary>
    /// Resolve <c>phone_number_id</c> → destino. Números inativos e destinos suspensos
    /// simplesmente não aparecem no resultado — é assim que a suspensão do canal funciona.
    /// </summary>
    Task<IReadOnlyDictionary<string, RotaDestino>> ResolverPorNumeroAsync(
        IReadOnlyCollection<string> phoneNumberIds, CancellationToken ct = default);

    /// <summary>
    /// Rota de reserva para eventos que não têm número (status de template, por exemplo):
    /// cai no destino dono do WABA. Se o WABA tiver números de mais de um destino, é ambíguo
    /// e nada é resolvido — melhor não entregar do que entregar para o município errado.
    /// </summary>
    Task<IReadOnlyDictionary<string, RotaDestino>> ResolverPorWabaAsync(
        IReadOnlyCollection<string> wabaIds, CancellationToken ct = default);
}
