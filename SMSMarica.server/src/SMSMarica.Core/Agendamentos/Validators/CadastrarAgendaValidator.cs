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

        // Consulta exige especialidade E médico (a agenda é do profissional);
        // Exame exige equipamento. Pools soltos de especialidade foram descontinuados.
        RuleFor(a => a.EspecialidadeId)
            .NotNull().WithMessage("Agenda de consulta exige especialidade.")
            .When(a => a.Finalidade == FinalidadeAgenda.Consulta);
        RuleFor(a => a.MedicoId)
            .NotNull().WithMessage("Agenda de consulta exige um médico — a agenda é do profissional.")
            .When(a => a.Finalidade == FinalidadeAgenda.Consulta);
        RuleFor(a => a.EquipamentoId)
            .NotNull().WithMessage("Agenda de exame exige equipamento.")
            .When(a => a.Finalidade == FinalidadeAgenda.Exame);

        // Grade semanal inicial (opcional): cada faixa precisa ser válida.
        RuleForEach(a => a.Recorrencias)
            .ChildRules(r =>
            {
                r.RuleFor(x => x.DiaSemana).IsInEnum();
                r.RuleFor(x => x.HoraFim)
                    .GreaterThan(x => x.HoraInicio)
                    .WithMessage("Na grade semanal, a hora de fim deve ser maior que a de início.");
            })
            .When(a => a.Recorrencias is not null);
    }
}
