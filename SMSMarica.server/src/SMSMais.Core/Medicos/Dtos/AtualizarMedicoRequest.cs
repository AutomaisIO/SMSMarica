using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Medicos.Dtos;

/// <summary>
/// Nome e CPF do médico são imutáveis (vêm do Usuario, definido no gate
/// inicial via consulta Receita).
/// </summary>
public sealed record AtualizarMedicoRequest(
    string Registro,
    string UfConselho,
    string Conselho = "CRM",
    string? Especialidade = null,
    string? Rqe = null,
    DateOnly? ValidadeRegistro = null,
    string? Telefone = null,
    EnderecoDto? Endereco = null,
    string? FotoBase64 = null);
