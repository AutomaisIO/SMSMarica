using FluentValidation;
using SMSMarica.Core.Agendamentos.Dtos;

namespace SMSMarica.Core.Agendamentos.Validators;

public sealed class AtualizarAgendaValidator : AbstractValidator<AtualizarAgendaRequest>
{
    public AtualizarAgendaValidator()
    {
        RuleFor(a => a.DuracaoSlotMinutos).InclusiveBetween(5, 240);
        RuleFor(a => a.VigenciaInicio).NotEmpty();
        RuleFor(a => a.VigenciaFim)
            .GreaterThanOrEqualTo(a => a.VigenciaInicio)
            .When(a => a.VigenciaFim.HasValue);
    }
}
