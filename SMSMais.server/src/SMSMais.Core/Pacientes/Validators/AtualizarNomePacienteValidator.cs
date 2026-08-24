using FluentValidation;
using SMSMais.Core.Pacientes.Dtos;

namespace SMSMais.Core.Pacientes.Validators;

public sealed class AtualizarNomePacienteValidator : AbstractValidator<AtualizarNomePacienteRequest>
{
    public AtualizarNomePacienteValidator()
    {
        RuleFor(p => p.NomeCompleto)
            .NotEmpty().WithMessage("Nome completo é obrigatório.")
            .MinimumLength(3).WithMessage("Nome completo deve ter ao menos 3 caracteres.")
            .MaximumLength(200);
    }
}
