using SMSMarica.Core.Common.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record UsuarioDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    string? Cpf,
    string? Telefone,
    EnderecoDto? Endereco,
    PerfilUsuario Perfil,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? UltimoAcessoEm);

public sealed record UsuarioListItemDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    PerfilUsuario Perfil,
    bool Ativo);
