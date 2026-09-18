using FluentValidation;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila.Validators;

public sealed class ParametroNumeroValidator : AbstractValidator<ParametroNumero>
{
    public ParametroNumeroValidator()
    {
        RuleFor(p => p.Valor).Must(v => !double.IsNaN(v) && !double.IsInfinity(v)).WithMessage("Valor inválido.");
        RuleFor(p => p.Valor).GreaterThanOrEqualTo(0);
        RuleFor(p => p).Must(p => p.Min is null || p.Max is null || p.Min <= p.Max)
            .WithMessage("Mínimo não pode ser maior que o máximo.");
    }
}

public sealed class ParametrosEstrategiaValidator : AbstractValidator<ParametrosEstrategia>
{
    public ParametrosEstrategiaValidator()
    {
        RuleFor(p => p.Objetivo).NotEmpty().Must(o => ObjetivoEstrategia.Todos.Contains(o))
            .WithMessage("Objetivo deve ser zerar_em_semanas, equilibrio ou minimo_recursos.");
        RuleFor(p => p.PrazoAlvoSemanas).GreaterThan(0).LessThanOrEqualTo(ParametrosEstrategia.HorizonteMaximo)
            .When(p => p.PrazoAlvoSemanas is not null);
        RuleFor(p => p.HorizonteSemanas).InclusiveBetween(0, ParametrosEstrategia.HorizonteMaximo);

        RuleFor(p => p.Unidades).NotNull().SetValidator(new ParametroNumeroValidator());
        RuleFor(p => p.Profissionais).NotNull().SetValidator(new ParametroNumeroValidator());
        RuleFor(p => p.TurnosPorProfissionalSemana).NotNull().SetValidator(new ParametroNumeroValidator());
        RuleFor(p => p.TurnosPorProfissionalSemana.Valor).LessThanOrEqualTo(14)
            .WithMessage("No máximo 14 turnos por semana por profissional (2 por dia).")
            .When(p => p.TurnosPorProfissionalSemana is not null);
        RuleFor(p => p.AtendimentosPorTurno).NotNull().SetValidator(new ParametroNumeroValidator());
        RuleFor(p => p.Aproveitamento).NotNull().SetValidator(new ParametroNumeroValidator());
        RuleFor(p => p.Aproveitamento.Valor).LessThanOrEqualTo(1).When(p => p.Aproveitamento is not null);
        RuleFor(p => p.EntradaSemanal).NotNull().SetValidator(new ParametroNumeroValidator());

        RuleForEach(p => p.Mutiroes).ChildRules(m =>
        {
            m.RuleFor(x => x.Semana).InclusiveBetween(1, ParametrosEstrategia.HorizonteMaximo);
            m.RuleFor(x => x.Vagas).InclusiveBetween(1, 100_000);
            m.RuleFor(x => x.Descricao).MaximumLength(200);
        }).When(p => p.Mutiroes is not null);
    }
}

public sealed class SimularRequestValidator : AbstractValidator<SimularRequest>
{
    public SimularRequestValidator()
    {
        RuleFor(r => r.ProcedimentoNome).NotEmpty().MaximumLength(300);
        RuleFor(r => r.ProcedimentoCodigo).MaximumLength(20);
        RuleFor(r => r.Parametros).SetValidator(new ParametrosEstrategiaValidator()!).When(r => r.Parametros is not null);
    }
}

public sealed class CriarEstrategiaRequestValidator : AbstractValidator<CriarEstrategiaRequest>
{
    public CriarEstrategiaRequestValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(200);
        RuleFor(r => r.ProcedimentoNome).NotEmpty().MaximumLength(300);
        RuleFor(r => r.ProcedimentoCodigo).MaximumLength(20);
        RuleFor(r => r.Parametros).SetValidator(new ParametrosEstrategiaValidator()!).When(r => r.Parametros is not null);
    }
}

public sealed class AtualizarEstrategiaRequestValidator : AbstractValidator<AtualizarEstrategiaRequest>
{
    public AtualizarEstrategiaRequestValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Parametros).NotNull().SetValidator(new ParametrosEstrategiaValidator());
        RuleFor(r => r.Status).IsInEnum().When(r => r.Status is not null);
    }
}

public sealed class NovaRodadaRequestValidator : AbstractValidator<NovaRodadaRequest>
{
    public NovaRodadaRequestValidator()
    {
        RuleFor(r => r.Modo).IsInEnum();
        RuleFor(r => r.Parametros).SetValidator(new ParametrosEstrategiaValidator()!).When(r => r.Parametros is not null);
    }
}

public sealed class MarcarAplicadaRequestValidator : AbstractValidator<MarcarAplicadaRequest>
{
    public MarcarAplicadaRequestValidator()
    {
        RuleFor(r => r.Nota).MaximumLength(2000);
    }
}
