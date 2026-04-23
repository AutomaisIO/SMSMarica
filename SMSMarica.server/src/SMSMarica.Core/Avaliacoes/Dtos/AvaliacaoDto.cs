namespace SMSMarica.Core.Avaliacoes.Dtos;

public sealed record AvaliacaoDto(
    Guid Id,
    Guid SessaoId,
    int Nota,
    string? Comentario,
    DateTime CriadoEm);

public sealed record AvaliacaoListItemDto(
    Guid Id,
    Guid SessaoId,
    int Nota);
