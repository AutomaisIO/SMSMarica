using FluentValidation;
using SMSMais.Core.TiposExame.Dtos;

namespace SMSMais.Core.TiposExame.Validators;

public sealed class AtualizarTipoExameValidator : AbstractValidator<AtualizarTipoExameRequest>
{
    public AtualizarTipoExameValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(200);
        // SIGTAP não é mais obrigatório: é correlação de faturamento (ver TipoExame).
        RuleFor(t => t.CodigoSisreg).MaximumLength(10);
        RuleFor(t => t.RequestedProcedureDescription).NotEmpty().MaximumLength(200);
        RuleFor(t => t.ScheduledProcedureStepDescription).NotEmpty().MaximumLength(200);
        RuleFor(t => t.TempoEstimadoMinutos)
            .InclusiveBetween(1, 600).When(t => t.TempoEstimadoMinutos.HasValue);
    }
}
