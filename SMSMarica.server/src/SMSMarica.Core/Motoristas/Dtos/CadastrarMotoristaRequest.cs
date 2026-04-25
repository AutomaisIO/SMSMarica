using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Motoristas.Dtos;

public sealed record CadastrarMotoristaRequest(
    string NomeCompleto,
    string Cpf,
    string Cnh,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);
