using FluentValidation;
using SMSMarica.Core.Translado.Dtos;

namespace SMSMarica.Core.Translado.Validators;

public sealed class CadastrarRotaValidator : AbstractValidator<CadastrarRotaRequest>
{
    public CadastrarRotaValidator()
    {
        RuleFor(r => r.Data).NotEmpty();
        RuleFor(r => r.VeiculoId).NotEmpty();
        RuleFor(r => r.MotoristaId).NotEmpty();
    }
}

public sealed class AtualizarRotaValidator : AbstractValidator<AtualizarRotaRequest>
{
    public AtualizarRotaValidator()
    {
        RuleFor(r => r.VeiculoId).NotEmpty();
        RuleFor(r => r.MotoristaId).NotEmpty();
    }
}
