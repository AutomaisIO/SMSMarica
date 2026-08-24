using FluentValidation;
using SMSMais.Core.Anexos.Dtos;

namespace SMSMais.Core.Anexos.Validators;

public sealed class SalvarAnexoValidator : AbstractValidator<SalvarAnexoDto>
{
    public SalvarAnexoValidator()
    {
        RuleFor(x => x.Nome)
            .MaximumLength(200).WithMessage("Nome deve ter no máximo 200 caracteres.")
            .When(x => x.Nome is not null);

        RuleFor(x => x.Descricao)
            .MaximumLength(2000).WithMessage("Descrição deve ter no máximo 2000 caracteres.")
            .When(x => x.Descricao is not null);
    }
}
