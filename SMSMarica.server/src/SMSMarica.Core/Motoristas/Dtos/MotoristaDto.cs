namespace SMSMarica.Core.Motoristas.Dtos;

public sealed record MotoristaDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    string Cnh,
    string? Telefone,
    bool Ativo,
    DateTime CriadoEm);

public sealed record MotoristaListItemDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    bool Ativo);
