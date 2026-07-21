using FluentValidation;
using SMSMarica.Core.Equipamentos.Dtos;
using SMSMarica.Core.Worklist;

namespace SMSMarica.Core.Equipamentos.Validators;

public sealed class CadastrarEquipamentoValidator : AbstractValidator<CadastrarEquipamentoRequest>
{
    public CadastrarEquipamentoValidator()
    {
        RuleFor(e => e.Nome).NotEmpty().MaximumLength(200);
        RuleFor(e => e.UnidadeId).NotEmpty();
        RuleFor(e => e.ModalidadeDicom).IsInEnum();
        RuleFor(e => e.IdentificadorDicom)
            .Must(ResolvedorEstacaoWorklist.AeTitleValido)
            .WithMessage("Identificador DICOM deve ser um AE Title válido: até 16 caracteres, sem espaços nem acentos.")
            .When(e => !string.IsNullOrWhiteSpace(e.IdentificadorDicom));
    }
}
