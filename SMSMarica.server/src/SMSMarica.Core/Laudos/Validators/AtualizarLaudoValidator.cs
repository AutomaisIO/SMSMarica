using FluentValidation;
using SMSMarica.Core.Laudos.Dtos;

namespace SMSMarica.Core.Laudos.Validators;

public sealed class AtualizarLaudoValidator : AbstractValidator<AtualizarLaudoRequest>
{
    public AtualizarLaudoValidator()
    {
        RuleFor(l => l.Titulo)
            .MaximumLength(200);

        RuleFor(l => l.ConteudoJson)
            .NotEmpty().WithMessage("Conteúdo (JSON) é obrigatório.");

        RuleFor(l => l.ConteudoHtml)
            .NotNull().WithMessage("Conteúdo (HTML) é obrigatório.");
    }
}
