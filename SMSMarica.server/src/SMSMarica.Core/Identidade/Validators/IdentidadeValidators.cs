using FluentValidation;
using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade.Validators;

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
    }
}
