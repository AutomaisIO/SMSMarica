using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Medicos.Dtos;

public sealed record CadastrarMedicoRequest(
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string Registro,
    string UfConselho,
    string Conselho = "CRM",
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeRegistro = null,
    string? Email = null,
    string? Telefone = null,
    EnderecoDto? Endereco = null,
    string? FotoBase64 = null);

public sealed record PromoverMedicoRequest(
    Guid UsuarioId,
    string Registro,
    string UfConselho,
    string Conselho = "CRM",
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeRegistro = null);
