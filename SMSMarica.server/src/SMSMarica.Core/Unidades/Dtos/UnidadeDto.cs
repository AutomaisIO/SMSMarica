namespace SMSMarica.Core.Unidades.Dtos;

public sealed record UnidadeDto(
    Guid Id,
    string Nome,
    string Endereco,
    string? Telefone,
    double Latitude,
    double Longitude,
    bool Ativo,
    DateTime CriadoEm);

public sealed record UnidadeListItemDto(
    Guid Id,
    string Nome,
    bool Ativo);
