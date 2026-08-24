using FluentValidation;
using SMSMais.Core.Veiculos.Dtos;

namespace SMSMais.Core.Veiculos.Validators;

public sealed class CadastrarVeiculoValidator : AbstractValidator<CadastrarVeiculoRequest>
{
    public CadastrarVeiculoValidator()
    {
        RuleFor(v => v.Placa).NotEmpty().MaximumLength(10);
        RuleFor(v => v.Modelo).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Fabricante).NotEmpty().MaximumLength(80);
        RuleFor(v => v.Cor).NotEmpty().MaximumLength(40);
        RuleFor(v => v.Tipo).IsInEnum();

        RuleFor(v => v.Fileiras)
            .NotNull()
            .Must(f => f.Count >= 1).WithMessage("Veículo deve ter pelo menos uma fileira.")
            .Must(f => f.Count <= 30).WithMessage("Veículo não pode ter mais de 30 fileiras.")
            .Must(f => f.Select(x => x.Ordem).Distinct().Count() == f.Count)
                .WithMessage("Ordens de fileira duplicadas.");

        RuleForEach(v => v.Fileiras).SetValidator(new FileiraInputValidator());
    }
}

public sealed class AtualizarVeiculoValidator : AbstractValidator<AtualizarVeiculoRequest>
{
    public AtualizarVeiculoValidator()
    {
        RuleFor(v => v.Placa).NotEmpty().MaximumLength(10);
        RuleFor(v => v.Modelo).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Fabricante).NotEmpty().MaximumLength(80);
        RuleFor(v => v.Cor).NotEmpty().MaximumLength(40);
        RuleFor(v => v.Tipo).IsInEnum();
    }
}

public sealed class FileiraInputValidator : AbstractValidator<FileiraInput>
{
    public FileiraInputValidator()
    {
        RuleFor(f => f.Ordem).GreaterThan(0).LessThanOrEqualTo(30);
        RuleFor(f => f.Assentos)
            .NotNull()
            .Must(a => a.Count >= 1).WithMessage("Fileira deve ter pelo menos um assento.")
            .Must(a => a.Count <= 10).WithMessage("Fileira não pode ter mais de 10 assentos.")
            .Must(a => a.Select(x => x.Numero).Distinct().Count() == a.Count)
                .WithMessage("Números de assento duplicados na fileira.");

        RuleForEach(f => f.Assentos).ChildRules(a =>
        {
            a.RuleFor(x => x.Numero).GreaterThan(0);
            a.RuleFor(x => x.Tipo).IsInEnum();
        });
    }
}

public sealed class AdicionarFileiraValidator : AbstractValidator<AdicionarFileiraRequest>
{
    public AdicionarFileiraValidator()
    {
        RuleFor(f => f.Ordem).GreaterThan(0);
        RuleFor(f => f.QuantidadeAssentos).InclusiveBetween(1, 10);
    }
}
