using FluentValidation;
using SMSMais.Core.Unidades.Dtos;

namespace SMSMais.Core.Unidades.Validators;

public sealed class CadastrarUnidadeValidator : AbstractValidator<CadastrarUnidadeRequest>
{
    public CadastrarUnidadeValidator()
    {
        RuleFor(u => u.Nome).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Telefone).MaximumLength(30);
        RuleFor(u => u.Latitude!).InclusiveBetween(-90, 90).When(u => u.Latitude is not null);
        RuleFor(u => u.Longitude!).InclusiveBetween(-180, 180).When(u => u.Longitude is not null);
    }
}

public sealed class AtualizarUnidadeValidator : AbstractValidator<AtualizarUnidadeRequest>
{
    public AtualizarUnidadeValidator()
    {
        RuleFor(u => u.Nome).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Telefone).MaximumLength(30);
        RuleFor(u => u.Latitude!).InclusiveBetween(-90, 90).When(u => u.Latitude is not null);
        RuleFor(u => u.Longitude!).InclusiveBetween(-180, 180).When(u => u.Longitude is not null);
    }
}
