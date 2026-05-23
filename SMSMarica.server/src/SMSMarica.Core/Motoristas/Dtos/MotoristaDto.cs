using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Motoristas.Dtos;

public sealed record MotoristaDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    string Cnh,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool Ativo,
    DateTime CriadoEm);

public sealed record MotoristaListItemDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    string? FotoBase64,
    bool Ativo);
