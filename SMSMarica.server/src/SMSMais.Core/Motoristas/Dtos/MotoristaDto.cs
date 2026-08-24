using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Motoristas.Dtos;

public sealed record MotoristaDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string Cnh,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool UsuarioAtivo,
    DateTime CriadoEm);

public sealed record MotoristaListItemDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    string? FotoBase64,
    bool UsuarioAtivo);
