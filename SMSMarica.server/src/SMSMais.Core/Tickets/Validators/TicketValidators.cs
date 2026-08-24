using FluentValidation;
using SMSMais.Core.Tickets.Dtos;

namespace SMSMais.Core.Tickets.Validators;

public sealed class AbrirTicketValidator : AbstractValidator<AbrirTicketRequest>
{
    public AbrirTicketValidator()
    {
        RuleFor(t => t.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(t => t.Descricao).NotEmpty().MaximumLength(5000);
        RuleFor(t => t.Tipo).IsInEnum();
        RuleForEach(t => t.Anexos).ChildRules(a =>
        {
            a.RuleFor(x => x.MidiaId).NotEmpty();
            a.RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(300);
        });
    }
}

public sealed class ComentarTicketValidator : AbstractValidator<ComentarTicketRequest>
{
    public ComentarTicketValidator()
    {
        RuleFor(c => c.Texto).NotEmpty().MaximumLength(5000);
        RuleForEach(c => c.Anexos).ChildRules(a =>
        {
            a.RuleFor(x => x.MidiaId).NotEmpty();
            a.RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(300);
        });
    }
}

public sealed class AtualizarTicketGestaoValidator : AbstractValidator<AtualizarTicketGestaoRequest>
{
    public AtualizarTicketGestaoValidator()
    {
        When(r => r.Status.HasValue, () => RuleFor(r => r.Status!.Value).IsInEnum());
        When(r => r.Prioridade.HasValue, () => RuleFor(r => r.Prioridade!.Value).IsInEnum());
        RuleFor(r => r.RespostaFinal).MaximumLength(5000);
    }
}

public sealed class AtualizarVisibilidadeValidator : AbstractValidator<AtualizarVisibilidadeRequest>
{
    public AtualizarVisibilidadeValidator()
    {
        RuleFor(r => r.Visibilidade).IsInEnum();
    }
}
