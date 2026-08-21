namespace Automais.Zap.Core.Entregas;

public sealed record ResultadoEntrega(bool Sucesso, int? StatusHttp, int DuracaoMs, string? Erro);

public interface IEntregador
{
    /// <summary>
    /// POST do corpo já pronto e já assinado no webhook da aplicação de destino.
    /// Nunca lança: falha de rede vira <see cref="ResultadoEntrega"/> com Sucesso=false.
    /// </summary>
    Task<ResultadoEntrega> EntregarAsync(string url, byte[] corpo, string assinatura, CancellationToken ct = default);
}
