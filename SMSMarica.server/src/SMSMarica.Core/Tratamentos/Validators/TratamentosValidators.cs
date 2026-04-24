using FluentValidation;
using SMSMarica.Core.Tratamentos.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos.Validators;

public sealed class CadastrarTratamentoValidator : AbstractValidator<CadastrarTratamentoRequest>
{
    public CadastrarTratamentoValidator()
    {
        RuleFor(t => t.PacienteId).NotEmpty();
        RuleFor(t => t.UnidadeId).NotEmpty();
        RuleFor(t => t.Descricao).NotEmpty().MaximumLength(500);
        RuleFor(t => t.CodigoSusLiberacao).MaximumLength(60);
        RuleFor(t => t.Periodicidade).NotNull().SetValidator(new CadastrarPeriodicidadeValidator());
    }
}

public sealed class CadastrarPeriodicidadeValidator : AbstractValidator<CadastrarPeriodicidadeRequest>
{
    public CadastrarPeriodicidadeValidator()
    {
        RuleFor(p => p.Tipo).IsInEnum();
        RuleFor(p => p.QuantidadeSessoes).InclusiveBetween(1, 365);

        When(p => p.Tipo == TipoPeriodicidade.IntervaloDias, () =>
            RuleFor(p => p.IntervaloDias)
                .NotNull()
                .InclusiveBetween(1, 365)
                .WithMessage("IntervaloDias é obrigatório quando Tipo = IntervaloDias."));

        When(p => p.Tipo == TipoPeriodicidade.SemanaDiasFixos, () =>
            RuleFor(p => p.DiasSemanaMascara)
                .NotNull()
                .InclusiveBetween(1, 127)
                .WithMessage("DiasSemanaMascara é obrigatório quando Tipo = SemanaDiasFixos (bitmask 1-127)."));
    }
}

public sealed class AtualizarTratamentoValidator : AbstractValidator<AtualizarTratamentoRequest>
{
    public AtualizarTratamentoValidator()
    {
        RuleFor(t => t.Descricao).NotEmpty().MaximumLength(500);
        RuleFor(t => t.CodigoSusLiberacao).MaximumLength(60);
    }
}

public sealed class AtualizarSessaoValidator : AbstractValidator<AtualizarSessaoRequest>
{
    public AtualizarSessaoValidator()
    {
        RuleFor(s => s.DataPrevista).NotEmpty();
    }
}

public sealed class AdicionarSessaoValidator : AbstractValidator<AdicionarSessaoRequest>
{
    public AdicionarSessaoValidator()
    {
        RuleFor(s => s.DataPrevista).NotEmpty();
    }
}

public sealed class ConfirmarSessaoValidator : AbstractValidator<ConfirmarSessaoRequest>
{
    public ConfirmarSessaoValidator()
    {
        RuleFor(s => s.NomeAcompanhante).MaximumLength(200);
        RuleFor(s => s.ParentescoAcompanhante).MaximumLength(60);
        RuleFor(s => s.MotivoNaoRealizacao).MaximumLength(500);
        When(s => !s.Realizada, () =>
            RuleFor(s => s.MotivoNaoRealizacao)
                .NotEmpty()
                .WithMessage("Informe o motivo da não realização."));
    }
}

public sealed class ExpandirPeriodicidadeValidator : AbstractValidator<ExpandirPeriodicidadeRequest>
{
    public ExpandirPeriodicidadeValidator()
    {
        RuleFor(p => p.Tipo).IsInEnum();
        RuleFor(p => p.QuantidadeSessoes).InclusiveBetween(1, 365);
    }
}
