using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Motoristas.Dtos;

public sealed record AtualizarMotoristaRequest(
    string NomeCompleto,
    string Cnh,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);
