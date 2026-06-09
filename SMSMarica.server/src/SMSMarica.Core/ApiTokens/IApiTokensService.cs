using SMSMarica.Core.ApiTokens.Dtos;

namespace SMSMarica.Core.ApiTokens;

public interface IApiTokensService
{
    Task<IReadOnlyList<ApiTokenListItemDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria um token novo. Retorna o valor em claro (mostrado uma única vez).</summary>
    Task<ApiTokenCriadoDto> CriarAsync(
        CriarApiTokenRequest request, Guid? usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Revoga (desativa) um token. Idempotente.</summary>
    Task RevogarAsync(Guid id, Guid? usuarioId, CancellationToken cancellationToken = default);
}
