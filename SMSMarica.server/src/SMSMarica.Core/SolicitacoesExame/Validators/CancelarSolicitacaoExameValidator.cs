using FluentValidation;
using SMSMarica.Core.SolicitacoesExame.Dtos;

namespace SMSMarica.Core.SolicitacoesExame.Validators;

public sealed class CancelarSolicitacaoExameValidator : AbstractValidator<CancelarSolicitacaoExameRequest>
{
    public CancelarSolicitacaoExameValidator()
    {
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}
