using FluentValidation;
using SMSMarica.Core.Perfis.Dtos;

namespace SMSMarica.Core.Perfis.Validators;

public sealed class CadastrarPerfilValidator : AbstractValidator<CadastrarPerfilRequest>
{
    public CadastrarPerfilValidator()
    {
        RuleFor(p => p.Nome).NotEmpty().MaximumLength(120);
        RuleFor(p => p.Descricao).MaximumLength(400);
    }
}

public sealed class AtualizarPerfilValidator : AbstractValidator<AtualizarPerfilRequest>
{
    public AtualizarPerfilValidator()
    {
        RuleFor(p => p.Nome).NotEmpty().MaximumLength(120);
        RuleFor(p => p.Descricao).MaximumLength(400);
    }
}
