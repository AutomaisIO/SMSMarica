using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Medicos.Dtos;

public sealed record AtualizarMedicoRequest(
    string NomeCompleto,
    string Crm,
    string UfCrm,
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeCrm = null,
    string? Telefone = null,
    EnderecoDto? Endereco = null,
    string? FotoBase64 = null);
