using FluentValidation;
using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade.Validators;

public sealed class CadastrarUsuarioValidator : AbstractValidator<CadastrarUsuarioRequest>
{
    public CadastrarUsuarioValidator()
    {
        RuleFor(u => u.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(u => u.Cpf)
            .Must(c => c is null || (c.All(char.IsDigit) && c.Length == 11))
            .WithMessage("CPF deve ter 11 dígitos quando informado.");
        RuleFor(u => u.Perfil).IsInEnum();
    }
}

public sealed class AtualizarUsuarioValidator : AbstractValidator<AtualizarUsuarioRequest>
{
    public AtualizarUsuarioValidator()
    {
        RuleFor(u => u.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Cpf)
            .Must(c => c is null || (c.All(char.IsDigit) && c.Length == 11))
            .WithMessage("CPF deve ter 11 dígitos quando informado.");
        RuleFor(u => u.Perfil).IsInEnum();
    }
}
