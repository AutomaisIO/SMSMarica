using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Admin atualiza dados de outro usuário. Nome, CPF e data de nascimento são
/// imutáveis após o cadastro (definidos no gate inicial via consulta Receita)
/// e não fazem parte deste request.
/// </summary>
public sealed record AtualizarUsuarioRequest(
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
