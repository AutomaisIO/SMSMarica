using FluentValidation;
using SMSMarica.Core.Equipamentos.Dtos;

namespace SMSMarica.Core.Equipamentos.Validators;

public sealed class CadastrarEquipamentoValidator : AbstractValidator<CadastrarEquipamentoRequest>
{
    public CadastrarEquipamentoValidator()
    {
        RuleFor(e => e.Nome).NotEmpty().MaximumLength(200);
        RuleFor(e => e.UnidadeId).NotEmpty();
        RuleFor(e => e.ModalidadeDicom).IsInEnum();
        RuleFor(e => e.IdentificadorDicom).MaximumLength(64).When(e => !string.IsNullOrWhiteSpace(e.IdentificadorDicom));
    }
}
