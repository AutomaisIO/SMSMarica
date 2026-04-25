using SMSMarica.Core.Common.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Cadastro de usuário sem senha — autenticação entra via ADR futuro.
/// Por enquanto, o <c>SenhaHash</c> é preenchido com placeholder e deve ser
/// substituído quando o módulo de auth for implementado.
/// </summary>
public sealed record CadastrarUsuarioRequest(
    string NomeCompleto,
    string Email,
    string? Cpf,
    string? Telefone,
    EnderecoDto? Endereco,
    PerfilUsuario Perfil);
