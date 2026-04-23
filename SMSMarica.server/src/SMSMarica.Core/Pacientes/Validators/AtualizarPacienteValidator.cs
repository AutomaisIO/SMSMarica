using FluentValidation;
using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes.Validators;

public sealed class AtualizarPacienteValidator : AbstractValidator<AtualizarPacienteRequest>
{
    public AtualizarPacienteValidator()
    {
        RuleFor(p => p.NomeCompleto)
            .NotEmpty().WithMessage("Nome completo é obrigatório.")
            .MaximumLength(200);

        RuleFor(p => p.Cns)
            .Must(c => c is null || (c.All(char.IsDigit) && c.Length == 15))
            .WithMessage("CNS deve ter 15 dígitos quando informado.");

        RuleFor(p => p.Latitude).InclusiveBetween(-90, 90);
        RuleFor(p => p.Longitude).InclusiveBetween(-180, 180);
    }
}
