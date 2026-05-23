using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Medicos.Dtos;

public sealed record MedicoDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    string Crm,
    string UfCrm,
    string? Especialidade,
    string? Rqe,
    DateOnly? ValidadeCrm,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool Ativo,
    DateTime CriadoEm);

public sealed record MedicoListItemDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    string Crm,
    string UfCrm,
    string? Especialidade,
    string? FotoBase64,
    bool Ativo);
