using FluentValidation;
using SMSMais.Core.TiposTratamento.Dtos;

namespace SMSMais.Core.TiposTratamento.Validators;

public sealed class CadastrarTipoTratamentoValidator : AbstractValidator<CadastrarTipoTratamentoRequest>
{
    public CadastrarTipoTratamentoValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(120);
        RuleFor(t => t.Codigo).NotEmpty().MaximumLength(60);
    }
}

public sealed class AtualizarTipoTratamentoValidator : AbstractValidator<AtualizarTipoTratamentoRequest>
{
    public AtualizarTipoTratamentoValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(120);
        RuleFor(t => t.Codigo).NotEmpty().MaximumLength(60);
    }
}
