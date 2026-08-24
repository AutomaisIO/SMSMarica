using FluentValidation;
using SMSMais.Core.SolicitacoesExame.Dtos;

namespace SMSMais.Core.SolicitacoesExame.Validators;

public sealed class CadastrarSolicitacaoExameValidator : AbstractValidator<CadastrarSolicitacaoExameRequest>
{
    public CadastrarSolicitacaoExameValidator()
    {
        RuleFor(s => s.PacienteId).NotEmpty();
        RuleFor(s => s.TipoExameId).NotEmpty();
        RuleFor(s => s.UnidadeId).NotEmpty();

        RuleFor(s => s.SolicitanteNome).NotEmpty().MaximumLength(200);

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
