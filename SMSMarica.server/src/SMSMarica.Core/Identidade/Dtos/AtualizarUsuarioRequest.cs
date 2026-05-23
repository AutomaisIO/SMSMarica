using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>Admin atualiza dados de outro usuário.</summary>
public sealed record AtualizarUsuarioRequest(
    string NomeCompleto,
    string? Cpf,
    DateOnly? DataNascimento,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);

/// <summary>
/// O próprio usuário atualiza só os campos que podem ser editados sem privilégio
/// administrativo: foto, telefone e endereço. Nome, CPF, data de nascimento e
/// e-mail ficam de fora.
/// </summary>
public sealed record AtualizarMinhaContaRequest(
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);
