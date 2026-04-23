using FluentValidation;
using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes.Validators;

public sealed class CadastrarPacienteValidator : AbstractValidator<CadastrarPacienteRequest>
{
    public CadastrarPacienteValidator()
    {
        RuleFor(p => p.NomeCompleto)
            .NotEmpty().WithMessage("Nome completo é obrigatório.")
            .MaximumLength(200);

        RuleFor(p => p.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(SoDigitos).WithMessage("CPF deve ter 11 dígitos.")
            .Must(c => c.Length == 11).WithMessage("CPF deve ter 11 dígitos.")
            .When(p => p.Cpf is not null);

        RuleFor(p => p.Cns)
            .Must(c => c is null || (SoDigitos(c) && c.Length == 15))
            .WithMessage("CNS deve ter 15 dígitos quando informado.");

        RuleFor(p => p.Latitude).InclusiveBetween(-90, 90);
        RuleFor(p => p.Longitude).InclusiveBetween(-180, 180);
    }

    private static bool SoDigitos(string valor) => valor.All(char.IsDigit);
}
