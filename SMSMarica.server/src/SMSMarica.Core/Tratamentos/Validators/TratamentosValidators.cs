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
    }
}
