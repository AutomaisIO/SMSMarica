namespace SMSMarica.Core.ApiTokens.Dtos;

/// <summary>Item da listagem de tokens. Nunca inclui o token em claro nem o hash.</summary>
public sealed record ApiTokenListItemDto(
    Guid Id,
    string Nome,
    string Prefixo,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? UltimoUsoEm,
    DateTime? RevogadoEm);

/// <summary>
/// Resposta da criação. <see cref="Token"/> é o valor em claro, mostrado UMA
/// ÚNICA VEZ — não há como recuperá-lo depois.
/// </summary>
public sealed record ApiTokenCriadoDto(
    Guid Id,
    string Nome,
    string Prefixo,
    string Token);

public sealed record CriarApiTokenRequest(string Nome);
