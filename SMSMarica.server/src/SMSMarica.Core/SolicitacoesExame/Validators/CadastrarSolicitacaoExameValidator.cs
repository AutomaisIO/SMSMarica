using FluentValidation;
using SMSMarica.Core.SolicitacoesExame.Dtos;

namespace SMSMarica.Core.SolicitacoesExame.Validators;

public sealed class CadastrarSolicitacaoExameValidator : AbstractValidator<CadastrarSolicitacaoExameRequest>
{
    public CadastrarSolicitacaoExameValidator()
    {
        RuleFor(s => s.PacienteId).NotEmpty();
        RuleFor(s => s.TipoExameId).NotEmpty();
        RuleFor(s => s.UnidadeId).NotEmpty();

        RuleFor(s => s.SolicitanteNome).NotEmpty().MaximumLength(200);
        RuleFor(s => s.SolicitanteCrm)
            .NotEmpty().WithMessage("CRM é obrigatório.")
            .Must(c => c.Any(char.IsDigit)).WithMessage("CRM deve conter dígitos.")
            .MaximumLength(20);
        RuleFor(s => s.SolicitanteUfCrm).NotEmpty().Length(2);

        RuleFor(s => s.NumeroRegulacaoSus).MaximumLength(40);
        RuleFor(s => s.Justificativa).MaximumLength(1000);
        RuleFor(s => s.Observacoes).MaximumLength(2000);
    }
}
