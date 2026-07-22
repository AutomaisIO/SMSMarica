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
    /// Motorista vem da linha 1:1 (ADR-0006); Medico é resolvido pelo CPF no hub FHIR
    /// (Practitioner), já que médico não é mais linha em usuario.
    /// </summary>
    string? PapelAtual,
    /// <summary>Registro do conselho quando o papel é Medico (ex.: "CRM 52702650/RJ"). Null caso contrário.</summary>
    string? RegistroProfissional = null,
    /// <summary>Nome de usuário para login, alternativa ao e-mail/CPF. Null quando não definido.</summary>
    string? Login = null,
    /// <summary>Enxerga todas as unidades, sem depender de vínculo em usuario_unidade.</summary>
    bool AcessoGlobal = false);

public sealed record UsuarioListItemDto(
    Guid Id,
    string NomeCompleto,
    string? Cpf,
    string? Email,
    string? FotoBase64,
    bool Ativo,
    bool DeveTrocarSenha);
