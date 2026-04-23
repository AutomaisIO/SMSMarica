using FluentValidation;
using SMSMarica.Core.Veiculos.Dtos;

namespace SMSMarica.Core.Veiculos.Validators;

public sealed class CadastrarVeiculoValidator : AbstractValidator<CadastrarVeiculoRequest>
{
    public CadastrarVeiculoValidator()
    {
        RuleFor(v => v.Placa).NotEmpty().MaximumLength(10);
        RuleFor(v => v.Modelo).NotEmpty().MaximumLength(100);
    }
}

public sealed class AtualizarVeiculoValidator : AbstractValidator<AtualizarVeiculoRequest>
{
    public AtualizarVeiculoValidator()
    {
        RuleFor(v => v.Placa).NotEmpty().MaximumLength(10);
        RuleFor(v => v.Modelo).NotEmpty().MaximumLength(100);
    }
}

public sealed class AdicionarFileiraValidator : AbstractValidator<AdicionarFileiraRequest>
{
    public AdicionarFileiraValidator()
    {
        RuleFor(f => f.Ordem).GreaterThan(0);
        RuleFor(f => f.QuantidadeAssentos).InclusiveBetween(1, 20);
    }
}
