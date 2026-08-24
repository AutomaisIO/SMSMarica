using FluentValidation;
using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Worklist;

namespace SMSMais.Core.Equipamentos.Validators;

public sealed class AtualizarEquipamentoValidator : AbstractValidator<AtualizarEquipamentoRequest>
{
    public AtualizarEquipamentoValidator()
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
