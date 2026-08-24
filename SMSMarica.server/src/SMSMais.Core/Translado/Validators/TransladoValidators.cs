using FluentValidation;
using SMSMais.Core.Translado.Dtos;

namespace SMSMais.Core.Translado.Validators;

public sealed class CadastrarRotaValidator : AbstractValidator<CadastrarRotaRequest>
{
    public CadastrarRotaValidator()
    {
        RuleFor(r => r.Data).NotEmpty();
        RuleFor(r => r.VeiculoId).NotEmpty();
        RuleFor(r => r.MotoristaId).NotEmpty();
    }
}

public sealed class AtualizarRotaValidator : AbstractValidator<AtualizarRotaRequest>
{
    public AtualizarRotaValidator()
    {
        RuleFor(r => r.Data).NotEmpty();
        RuleFor(r => r.VeiculoId).NotEmpty();
        RuleFor(r => r.MotoristaId).NotEmpty();
    }
}

public sealed class CriarAlocacaoValidator : AbstractValidator<CriarAlocacaoRequest>
{
    public CriarAlocacaoValidator()
    {
        RuleFor(r => r.SessaoId).NotEmpty();
        RuleFor(r => r.AssentoId).NotEmpty();
    }
}
