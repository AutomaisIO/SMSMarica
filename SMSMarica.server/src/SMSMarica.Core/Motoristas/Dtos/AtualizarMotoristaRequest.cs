using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Motoristas.Dtos;

/// <summary>
/// Nome e CPF do motorista são imutáveis (vêm do Usuario, definido no gate
/// inicial via consulta Receita).
/// </summary>
public sealed record AtualizarMotoristaRequest(
    string Cnh,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);
