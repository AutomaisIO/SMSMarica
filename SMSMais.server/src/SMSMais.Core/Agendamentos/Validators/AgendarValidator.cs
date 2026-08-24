using FluentValidation;
using SMSMais.Core.Agendamentos.Dtos;

namespace SMSMais.Core.Agendamentos.Validators;

public sealed class AgendarValidator : AbstractValidator<AgendarRequest>
{
    public AgendarValidator()
    {
        RuleFor(a => a.AgendaId).NotEmpty();
        RuleFor(a => a.PacienteId).NotEmpty();
        RuleFor(a => a.InicioEm).NotEmpty();
        RuleFor(a => a.Observacao).MaximumLength(1000).When(a => !string.IsNullOrWhiteSpace(a.Observacao));
    }
}
