using FluentValidation;
using SMSMarica.Core.Avaliacoes.Dtos;

namespace SMSMarica.Core.Avaliacoes.Validators;

public sealed class RegistrarAvaliacaoValidator : AbstractValidator<RegistrarAvaliacaoRequest>
{
    public RegistrarAvaliacaoValidator()
    {
        RuleFor(a => a.SessaoId).NotEmpty();
        RuleFor(a => a.Nota).InclusiveBetween(1, 5);
        RuleFor(a => a.Comentario).MaximumLength(2000);
    }
}

public sealed class AtualizarAvaliacaoValidator : AbstractValidator<AtualizarAvaliacaoRequest>
{
    public AtualizarAvaliacaoValidator()
    {
        RuleFor(a => a.Nota).InclusiveBetween(1, 5);
        RuleFor(a => a.Comentario).MaximumLength(2000);
    }
}
