using FluentValidation;
using SMSMarica.Core.SolicitacoesExame.Dtos;

namespace SMSMarica.Core.SolicitacoesExame.Validators;

public sealed class AtualizarSolicitacaoExameValidator : AbstractValidator<AtualizarSolicitacaoExameRequest>
{
    public AtualizarSolicitacaoExameValidator()
    {
        RuleFor(s => s.TipoExameId).NotEmpty();
        RuleFor(s => s.UnidadeId).NotEmpty();
        RuleFor(s => s.SolicitanteNome).NotEmpty().MaximumLength(200);
        RuleFor(s => s.SolicitanteCrm)
            .NotEmpty()
            .Must(c => c.Any(char.IsDigit)).WithMessage("O registro (CRM/COREN) deve conter dígitos.")
            .MaximumLength(20);
        RuleFor(s => s.SolicitanteUfCrm).NotEmpty().Length(2);
        RuleFor(s => s.SolicitanteConselho).MaximumLength(20);
        RuleFor(s => s.CodigoSolicitacao)
            .NotEmpty().WithMessage("Código de Solicitação é obrigatório.")
            .Must(RegulacaoRegras.Valido).WithMessage(RegulacaoRegras.MensagemInvalido)
            .MaximumLength(50);
        RuleFor(s => s.ChaveConfirmacao)
            .NotEmpty().WithMessage("Chave de Confirmação é obrigatória.")
            .Must(RegulacaoRegras.Valido).WithMessage(RegulacaoRegras.MensagemInvalido)
            .MaximumLength(100);
        RuleFor(s => s.Justificativa).MaximumLength(1000);
        RuleFor(s => s.Observacoes).MaximumLength(2000);
    }
}
