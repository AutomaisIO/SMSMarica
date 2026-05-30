using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record CadastrarUsuarioRequest(
    string NomeCompleto,
    string Email,
    string? Cpf,
    DateOnly? DataNascimento,
    string? Telefone,
    EnderecoDto? Endereco,
    IReadOnlyList<Guid>? PerfilIds = null,
    string? Senha = null,
    string? FotoBase64 = null);
