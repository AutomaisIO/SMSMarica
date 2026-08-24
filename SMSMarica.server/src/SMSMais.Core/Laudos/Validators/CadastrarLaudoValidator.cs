using FluentValidation;
using SMSMais.Core.Laudos.BiRads;
using SMSMais.Core.Laudos.Dtos;

namespace SMSMais.Core.Laudos.Validators;

public sealed class CadastrarLaudoValidator : AbstractValidator<CadastrarLaudoRequest>
{
    public CadastrarLaudoValidator()
    {
        RuleFor(l => l.StudyInstanceUID)
            .NotEmpty().WithMessage("StudyInstanceUID é obrigatório.")
            .MaximumLength(128);

        RuleFor(l => l.Titulo)
            .MaximumLength(200);

        RuleFor(l => l.ConteudoJson)
            .NotEmpty().WithMessage("Conteúdo (JSON) é obrigatório.");

        RuleFor(l => l.ConteudoHtml)
            .NotNull().WithMessage("Conteúdo (HTML) é obrigatório.");

        When(l => l.Checklist?.BiRadsFinal is not null, () =>
            RuleFor(l => l.Checklist!.BiRadsFinal)
                .Must(CalculadoraBiRads.EhCategoriaValida)
                .WithMessage("Categoria BI-RADS inválida (use 0, 1, 2, 3, 4, 4A, 4B, 4C, 5 ou 6)."));
    }
}
