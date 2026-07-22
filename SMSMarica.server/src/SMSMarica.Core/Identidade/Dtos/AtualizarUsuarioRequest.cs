using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Admin atualiza dados de outro usuário. Nome, CPF e data de nascimento são
/// imutáveis após o cadastro (definidos no gate inicial via consulta Receita).
/// O e-mail PODE ser editado/inserido aqui — médicos importados vêm sem e-mail
/// e precisam recebê-lo para o login por e-mail (o login por CPF já funciona).
/// E-mail em branco é ignorado (não apaga o existente).
/// </summary>
public sealed record AtualizarUsuarioRequest(
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null,
    string? Email = null,
    /// <summary>Nome de usuário para login. Em branco = não mexe no atual.</summary>
    string? Login = null,
    /// <summary>
    /// Enxergar todas as unidades. <c>null</c> = não mexe. Só quem já tem acesso global
    /// consegue conceder ou revogar.
    /// </summary>
    bool? AcessoGlobal = null);

/// <summary>
/// O próprio usuário atualiza só os campos que podem ser editados sem privilégio
/// administrativo: foto, telefone e endereço. Nome, CPF, data de nascimento e
/// e-mail ficam de fora.
/// </summary>
public sealed record AtualizarMinhaContaRequest(
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null);
