using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record AtualizarUsuarioRequest(
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);
