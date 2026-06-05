using FluentValidation;
using SMSMarica.Core.Agendamentos.Dtos;

namespace SMSMarica.Core.Agendamentos.Validators;

public sealed class CadastrarAgendaValidator : AbstractValidator<CadastrarAgendaRequest>
{
    public CadastrarAgendaValidator()
    {
        RuleFor(a => a.UnidadeId).NotEmpty();
        RuleFor(a => a.EspecialidadeId).NotEmpty();
        RuleFor(a => a.MedicoId).NotEmpty();
        RuleFor(a => a.DuracaoConsultaMinutos).InclusiveBetween(5, 240);
        RuleFor(a => a.VigenciaInicio).NotEmpty();
        RuleFor(a => a.VigenciaFim)
            .GreaterThanOrEqualTo(a => a.VigenciaInicio)
            .When(a => a.VigenciaFim.HasValue);
    }
}
