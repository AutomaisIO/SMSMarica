using FluentValidation;
using SMSMais.Core.Solicitacoes.Dtos;

namespace SMSMais.Core.Solicitacoes.Validators;

public sealed class CancelarSolicitacaoValidator : AbstractValidator<CancelarSolicitacaoRequest>
{
    public CancelarSolicitacaoValidator()
    {
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}
