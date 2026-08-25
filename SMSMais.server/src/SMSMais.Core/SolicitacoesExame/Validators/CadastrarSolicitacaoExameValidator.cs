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
        // Chave de Confirmação é OPCIONAL na criação manual (ticket #77): a chave só é conhecida
        // quando o paciente chega ao balcão, e é cobrada/gravada na autorização (AutorizarAsync).
        // Espelha o caminho automático (importação SISREG), que cria a solicitação sem chave.
        // Quando informada, precisa seguir a régua da regulação (0000 ou >= 9999).
        RuleFor(s => s.ChaveConfirmacao)
            .Must(v => string.IsNullOrWhiteSpace(v) || RegulacaoRegras.Valido(v))
            .WithMessage(RegulacaoRegras.MensagemInvalido)
            .MaximumLength(100);
        RuleFor(s => s.Justificativa).MaximumLength(1000);
        RuleFor(s => s.Observacoes).MaximumLength(2000);
    }
}
