using FluentValidation;
using SMSMarica.Core.TiposExame.Dtos;

namespace SMSMarica.Core.TiposExame.Validators;

public sealed class CadastrarTipoExameValidator : AbstractValidator<CadastrarTipoExameRequest>
{
    public CadastrarTipoExameValidator()
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
