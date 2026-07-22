using System.Text.RegularExpressions;
using FluentValidation;
using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade.Validators;

/// <summary>
/// Formato do nome de usuário. Ele divide o campo da tela de entrada com e-mail e CPF, então
/// não pode parecer nenhum dos dois: nada de "@" e nunca só dígitos.
/// </summary>
internal static partial class RegrasLogin
{
    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,40}$")]
    private static partial Regex Formato();

    public static bool Valido(string? login) =>
        string.IsNullOrWhiteSpace(login)
        || (Formato().IsMatch(login) && !login.All(char.IsAsciiDigit));

    public const string Mensagem =
        "Nome de usuário deve ter de 3 a 40 caracteres (letras, números, ponto, hífen ou _) "
        + "e não pode ser só números.";
}

public sealed class CadastrarUsuarioValidator : AbstractValidator<CadastrarUsuarioRequest>
{
    public CadastrarUsuarioValidator()
    {
        RuleFor(u => u.NomeCompleto).NotEmpty().MaximumLength(200);
        // E-mail é opcional (médicos importados não têm), mas válido quando informado.
        RuleFor(u => u.Email)
            .EmailAddress().MaximumLength(200)
            .When(u => !string.IsNullOrWhiteSpace(u.Email));
        RuleFor(u => u.Senha)
            .MinimumLength(8).MaximumLength(200)
            .When(u => !string.IsNullOrEmpty(u.Senha));
        RuleFor(u => u.Cpf)
            .Must(c => c is null || (c.All(char.IsDigit) && c.Length == 11))
            .WithMessage("CPF deve ter 11 dígitos quando informado.");
        RuleFor(u => u.Login).Must(RegrasLogin.Valido).WithMessage(RegrasLogin.Mensagem);
        // Sem e-mail e sem CPF não há identificador de login.
        RuleFor(u => u)
            .Must(u => !string.IsNullOrWhiteSpace(u.Email) || !string.IsNullOrWhiteSpace(u.Cpf))
            .WithMessage("Informe e-mail ou CPF para o login.");
    }
}

public sealed class AtualizarUsuarioValidator : AbstractValidator<AtualizarUsuarioRequest>
{
    public AtualizarUsuarioValidator()
    {
        RuleFor(u => u.Telefone).MaximumLength(30);
        RuleFor(u => u.Email)
            .EmailAddress().MaximumLength(200)
            .When(u => !string.IsNullOrWhiteSpace(u.Email));
        RuleFor(u => u.Login).Must(RegrasLogin.Valido).WithMessage(RegrasLogin.Mensagem);
    }
}
