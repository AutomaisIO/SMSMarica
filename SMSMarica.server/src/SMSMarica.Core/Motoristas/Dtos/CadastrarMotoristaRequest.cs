using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Motoristas.Dtos;

public sealed record CadastrarMotoristaRequest(
    string NomeCompleto,
    string Cpf,
    string Cnh,
    string? Email,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);

public sealed record PromoverMotoristaRequest(
    Guid UsuarioId,
    string Cnh);
