using FluentValidation;
using SMSMais.Core.Perfis.Dtos;

namespace SMSMais.Core.Perfis.Validators;

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
