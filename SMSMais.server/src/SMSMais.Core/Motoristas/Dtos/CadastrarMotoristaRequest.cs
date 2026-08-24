using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Motoristas.Dtos;

public sealed record CadastrarMotoristaRequest(
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string Cnh,
    string? Email,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);

public sealed record PromoverMotoristaRequest(
    Guid UsuarioId,
    string Cnh);
