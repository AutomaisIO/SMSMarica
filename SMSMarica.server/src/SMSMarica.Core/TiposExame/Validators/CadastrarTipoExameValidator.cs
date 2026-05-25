using FluentValidation;
using SMSMarica.Core.TiposExame.Dtos;

namespace SMSMarica.Core.TiposExame.Validators;

public sealed class CadastrarTipoExameValidator : AbstractValidator<CadastrarTipoExameRequest>
{
    public CadastrarTipoExameValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(200);
        RuleFor(t => t.ProcedimentoSigtapId).NotEmpty();
        RuleFor(t => t.RequestedProcedureDescription).NotEmpty().MaximumLength(200);
        RuleFor(t => t.ScheduledProcedureStepDescription).NotEmpty().MaximumLength(200);
        RuleFor(t => t.TempoEstimadoMinutos)
            .InclusiveBetween(1, 600).When(t => t.TempoEstimadoMinutos.HasValue);
    }
}
