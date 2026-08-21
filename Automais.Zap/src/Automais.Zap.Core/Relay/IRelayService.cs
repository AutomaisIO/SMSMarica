namespace Automais.Zap.Core.Relay;

public enum SituacaoRelay
{
    /// <summary>Tudo que tinha rota foi entregue. Responder 200 à Meta.</summary>
    Ok = 1,

    /// <summary>Assinatura ausente ou divergente. 401 — e não processa nada.</summary>
    AssinaturaInvalida = 2,

    /// <summary>Corpo ilegível. 400.</summary>
    PayloadInvalido = 3,

    /// <summary>Sem App Secret configurado. 503 — falha fechado, nunca aceita sem conferir.</summary>
    NaoConfigurado = 4,

    /// <summary>Ao menos um destino recusou ou não respondeu. 5xx para a Meta reentregar.</summary>
    FalhaDeEntrega = 5,
}

public sealed record ResultadoRelay(SituacaoRelay Situacao, int Entregues, int Falhas, int SemRota);

public interface IRelayService
{
    Task<ResultadoRelay> ProcessarAsync(byte[] corpo, string? assinatura, CancellationToken ct = default);
}
