using FluentValidation;
using SMSMarica.Core.Identidade.Dtos;

namespace SMSMarica.Core.Identidade.Validators;

public sealed class CadastrarUsuarioValidator : AbstractValidator<CadastrarUsuarioRequest>
{
    public CadastrarUsuarioValidator()
    {
        RuleFor(u => u.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(u => u.Senha)
            .MinimumLength(8).MaximumLength(200)
            .When(u => !string.IsNullOrEmpty(u.Senha));
    }
}

public sealed class AtualizarUsuarioValidator : AbstractValidator<AtualizarUsuarioRequest>
{
    public AtualizarUsuarioValidator()
    {
        RuleFor(u => u.NomeCompleto).NotEmpty().MaximumLength(200);
    }
}

public sealed class AtualizarMinhaContaValidator : AbstractValidator<AtualizarMinhaContaRequest>
{
    public AtualizarMinhaContaValidator()
    {
        RuleFor(u => u.NomeCompleto).NotEmpty().MaximumLength(200);
    }
}
