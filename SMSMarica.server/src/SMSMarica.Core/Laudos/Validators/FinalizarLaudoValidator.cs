using FluentValidation;
using SMSMarica.Core.Laudos.BiRads;
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

        When(l => l.Checklist?.BiRadsFinal is not null, () =>
            RuleFor(l => l.Checklist!.BiRadsFinal)
                .Must(CalculadoraBiRads.EhCategoriaValida)
                .WithMessage("Categoria BI-RADS inválida (use 0, 1, 2, 3, 4, 4A, 4B, 4C, 5 ou 6)."));
    }
}
