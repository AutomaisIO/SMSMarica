namespace Automais.Zap.Core.Entregas;

public sealed record ResultadoEntrega(bool Sucesso, int? StatusHttp, int DuracaoMs, string? Erro);

public interface IEntregador
{
    /// <summary>
    /// POST do corpo já pronto e já assinado no webhook da aplicação de destino.
    /// Nunca lança: falha de rede vira <see cref="ResultadoEntrega"/> com Sucesso=false.
    /// </summary>
    /// <param name="assinatura">
    /// Assinatura da Meta, repassada quando o destino não tem segredo próprio.
    /// </param>
    /// <param name="segredoProprio">
    /// Quando presente, o relay assina com ELE em X-Automais-Signature em vez de repassar a
    /// da Meta — que só validaria se os dois lados usassem o mesmo App Secret.
    /// </param>
    Task<ResultadoEntrega> EntregarAsync(
        string url, byte[] corpo, string assinatura, string? segredoProprio = null, CancellationToken ct = default);
}
