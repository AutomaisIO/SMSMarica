using SMSMarica.Core.Common.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record UsuarioDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    string? Cpf,
    DateOnly? DataNascimento,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? UltimoAcessoEm,
    IReadOnlyList<Guid> PerfilIds,
    bool DeveTrocarSenha,
    TipoPapel? TipoPapel);

public sealed record UsuarioListItemDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    string? FotoBase64,
    bool Ativo,
    bool DeveTrocarSenha);
