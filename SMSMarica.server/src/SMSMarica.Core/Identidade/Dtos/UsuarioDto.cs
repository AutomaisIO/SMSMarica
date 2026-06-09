using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record UsuarioDto(
    Guid Id,
    string NomeCompleto,
    string? Email,
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
    /// <summary>
    /// Papel ativo do usuário (Medico/Motorista/Paciente), ou null se não tem papel.
    /// Derivado da existência de linha 1:1 nas tabelas correspondentes — ADR-0006.
    /// </summary>
    string? PapelAtual);

public sealed record UsuarioListItemDto(
    Guid Id,
    string NomeCompleto,
    string? Email,
    string? FotoBase64,
    bool Ativo,
    bool DeveTrocarSenha);
