using FluentValidation;
using SMSMais.Core.TiposTratamento.Dtos;

namespace SMSMais.Core.TiposTratamento.Validators;

public sealed class CadastrarTipoTratamentoValidator : AbstractValidator<CadastrarTipoTratamentoRequest>
{
    public CadastrarTipoTratamentoValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(120);
        RuleFor(t => t.Codigo).NotEmpty().MaximumLength(60);
        RuleFor(t => t.TempoMedioMinutos).TempoMedioValido();
    }
}

public sealed class AtualizarTipoTratamentoValidator : AbstractValidator<AtualizarTipoTratamentoRequest>
{
    public AtualizarTipoTratamentoValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(120);
        RuleFor(t => t.Codigo).NotEmpty().MaximumLength(60);
        RuleFor(t => t.TempoMedioMinutos).TempoMedioValido();
    }
}

internal static class TempoMedioRegra
{
    /// <summary>Tempo médio é obrigatório e cabe num dia (1 min a 24 h) — é a base da previsão
    /// de volta na rota.</summary>
    public static IRuleBuilderOptions<T, int?> TempoMedioValido<T>(this IRuleBuilder<T, int?> regra) =>
        regra
            .NotNull().WithMessage("Informe o tempo médio do tratamento.")
            .InclusiveBetween(1, 24 * 60).WithMessage("Tempo médio deve ficar entre 1 minuto e 24 horas.");
}
