using FluentValidation;
using SMSMarica.Core.Laudos.Dtos;

namespace SMSMarica.Core.Laudos.Validators;

public sealed class FinalizarLaudoValidator : AbstractValidator<FinalizarLaudoRequest>
{
    public FinalizarLaudoValidator()
    {
        RuleFor(l => l.Titulo)
            .MaximumLength(200);

        RuleFor(l => l.ConteudoJson)
            .NotEmpty().WithMessage("Conteúdo (JSON) é obrigatório.");

        RuleFor(l => l.ConteudoHtml)
            .NotEmpty().WithMessage("Conteúdo (HTML) é obrigatório para finalizar.");
    }
}
