using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Medicos.Dtos;

public sealed record MedicoDto(
    Guid Id,
    // Usuario (login) vinculado por CPF — null quando o médico não tem usuário de acesso.
    // NÃO é o id do Practitioner: resolvido em MedicosService pelo CPF.
    Guid? UsuarioId,
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string Conselho,
    string Registro,
    string UfConselho,
    string? Especialidade,
    string? Rqe,
    DateOnly? ValidadeRegistro,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool UsuarioAtivo,
    DateTime CriadoEm);

public sealed record MedicoListItemDto(
    Guid Id,
    Guid? UsuarioId,
    string NomeCompleto,
    string Cpf,
    string Conselho,
    string Registro,
    string UfConselho,
    string? Especialidade,
    string? FotoBase64,
    bool UsuarioAtivo);
