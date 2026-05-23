using System.Text.Json;
using FluentValidation;
using SMSMarica.Core.EstudoAnotacoes.Dtos;

namespace SMSMarica.Core.EstudoAnotacoes.Validators;

public sealed class SalvarEstudoAnotacaoValidator : AbstractValidator<SalvarEstudoAnotacaoRequest>
{
    public SalvarEstudoAnotacaoValidator()
    {
        // O payload precisa ser um objeto JSON (estado do Cornerstone). Strings/números/null não fazem sentido.
        RuleFor(r => r.Payload)
            .Must(p => p.ValueKind == JsonValueKind.Object || p.ValueKind == JsonValueKind.Array)
            .WithMessage("Payload precisa ser um objeto ou array JSON com o estado das anotações.");

        RuleFor(r => r.Comentario).MaximumLength(500);
    }
}
