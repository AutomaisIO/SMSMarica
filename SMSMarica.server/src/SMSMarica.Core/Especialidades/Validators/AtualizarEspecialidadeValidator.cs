using FluentValidation;
using SMSMarica.Core.Especialidades.Dtos;

namespace SMSMarica.Core.Especialidades.Validators;

public sealed class AtualizarEspecialidadeValidator : AbstractValidator<AtualizarEspecialidadeRequest>
{
    public AtualizarEspecialidadeValidator()
    {
        RuleFor(e => e.Nome).NotEmpty().MaximumLength(200);
        RuleFor(e => e.CodigoCbo).MaximumLength(10).When(e => !string.IsNullOrWhiteSpace(e.CodigoCbo));
    }
}
