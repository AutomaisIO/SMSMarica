using FluentValidation;
using SMSMais.Core.LaudoTemplates.Dtos;

namespace SMSMais.Core.LaudoTemplates.Validators;

public sealed class CadastrarLaudoTemplateValidator : AbstractValidator<CadastrarLaudoTemplateRequest>
{
    public CadastrarLaudoTemplateValidator()
    {
        RuleFor(t => t.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(200);

        RuleFor(t => t.Categoria)
            .NotEmpty().WithMessage("Categoria é obrigatória.")
            .MaximumLength(80);

        RuleFor(t => t.Descricao)
            .MaximumLength(500);

        RuleFor(t => t.ConteudoJson)
            .NotEmpty().WithMessage("Conteúdo (JSON) é obrigatório.");

        RuleFor(t => t.ConteudoHtml)
            .NotNull().WithMessage("Conteúdo (HTML) é obrigatório.");
    }
}
