using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Medicos.Dtos;

public sealed record CadastrarMedicoRequest(
    string NomeCompleto,
    string Cpf,
    string Crm,
    string UfCrm,
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeCrm = null,
    string? Email = null,
    string? Telefone = null,
    EnderecoDto? Endereco = null,
    string? FotoBase64 = null);

public sealed record PromoverMedicoRequest(
    Guid UsuarioId,
    string Crm,
    string UfCrm,
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeCrm = null);
