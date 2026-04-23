using FluentValidation;
using SMSMarica.Core.Motoristas.Dtos;

namespace SMSMarica.Core.Motoristas.Validators;

public sealed class CadastrarMotoristaValidator : AbstractValidator<CadastrarMotoristaRequest>
{
    public CadastrarMotoristaValidator()
    {
        RuleFor(m => m.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(m => m.Cpf)
            .NotEmpty()
            .Must(c => c.All(char.IsDigit) && c.Length == 11)
            .WithMessage("CPF deve ter 11 dígitos.");
        RuleFor(m => m.Cnh)
            .NotEmpty()
            .MaximumLength(11);
        RuleFor(m => m.Telefone).MaximumLength(30);
    }
}

public sealed class AtualizarMotoristaValidator : AbstractValidator<AtualizarMotoristaRequest>
{
    public AtualizarMotoristaValidator()
    {
        RuleFor(m => m.NomeCompleto).NotEmpty().MaximumLength(200);
        RuleFor(m => m.Cnh).NotEmpty().MaximumLength(11);
        RuleFor(m => m.Telefone).MaximumLength(30);
    }
}
