using FluentValidation;
using SMSMais.Core.Agendamentos.Dtos;

namespace SMSMais.Core.Agendamentos.Validators;

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
