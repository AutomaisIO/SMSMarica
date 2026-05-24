using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Medicos.Dtos;

/// <summary>
/// Nome e CPF do médico são imutáveis (vêm do Usuario, definido no gate
/// inicial via consulta Receita).
/// </summary>
public sealed record AtualizarMedicoRequest(
    string Crm,
    string UfCrm,
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeCrm = null,
    string? Telefone = null,
    EnderecoDto? Endereco = null,
    string? FotoBase64 = null);
