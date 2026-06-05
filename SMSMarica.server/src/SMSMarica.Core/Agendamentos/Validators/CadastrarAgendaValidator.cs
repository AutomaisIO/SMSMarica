using FluentValidation;
using SMSMarica.Core.Agendamentos.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Agendamentos.Validators;

public sealed class CadastrarAgendaValidator : AbstractValidator<CadastrarAgendaRequest>
{
    public CadastrarAgendaValidator()
    {
        RuleFor(a => a.Finalidade).IsInEnum();
        RuleFor(a => a.UnidadeId).NotEmpty();
        RuleFor(a => a.DuracaoSlotMinutos).InclusiveBetween(5, 240);
        RuleFor(a => a.VigenciaInicio).NotEmpty();
        RuleFor(a => a.VigenciaFim)
            .GreaterThanOrEqualTo(a => a.VigenciaInicio)
            .When(a => a.VigenciaFim.HasValue);

        // Consulta exige especialidade (médico é opcional = pool); Exame exige equipamento.
        RuleFor(a => a.EspecialidadeId)
            .NotNull().WithMessage("Agenda de consulta exige especialidade.")
            .When(a => a.Finalidade == FinalidadeAgenda.Consulta);
        RuleFor(a => a.EquipamentoId)
            .NotNull().WithMessage("Agenda de exame exige equipamento.")
            .When(a => a.Finalidade == FinalidadeAgenda.Exame);
    }
}
