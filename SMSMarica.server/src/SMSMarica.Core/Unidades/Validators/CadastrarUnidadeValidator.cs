using FluentValidation;
using SMSMarica.Core.Unidades.Dtos;

namespace SMSMarica.Core.Unidades.Validators;

public sealed class CadastrarUnidadeValidator : AbstractValidator<CadastrarUnidadeRequest>
{
    public CadastrarUnidadeValidator()
    {
        RuleFor(u => u.Nome).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Endereco).NotEmpty().MaximumLength(500);
        RuleFor(u => u.Telefone).MaximumLength(30);
        RuleFor(u => u.Latitude).InclusiveBetween(-90, 90);
        RuleFor(u => u.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class AtualizarUnidadeValidator : AbstractValidator<AtualizarUnidadeRequest>
{
    public AtualizarUnidadeValidator()
    {
        RuleFor(u => u.Nome).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Endereco).NotEmpty().MaximumLength(500);
        RuleFor(u => u.Telefone).MaximumLength(30);
        RuleFor(u => u.Latitude).InclusiveBetween(-90, 90);
        RuleFor(u => u.Longitude).InclusiveBetween(-180, 180);
    }
}
