using FluentValidation;
using SMSMais.Core.SolicitacoesExame.Dtos;

namespace SMSMais.Core.SolicitacoesExame.Validators;

public sealed class CancelarSolicitacaoExameValidator : AbstractValidator<CancelarSolicitacaoExameRequest>
{
    public CancelarSolicitacaoExameValidator()
    {
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}
