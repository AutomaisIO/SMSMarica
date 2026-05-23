using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record UsuarioDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    string? Cpf,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? UltimoAcessoEm,
    IReadOnlyList<Guid> PerfilIds);

public sealed record UsuarioListItemDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    string? FotoBase64,
    bool Ativo);
