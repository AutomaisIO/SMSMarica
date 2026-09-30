using FluentValidation;
using SMSMais.Core.Tratamentos.Dtos;

namespace SMSMais.Core.Tratamentos.Validators;

public sealed class CadastrarTratamentoValidator : AbstractValidator<CadastrarTratamentoRequest>
{
    public CadastrarTratamentoValidator()
    {
        RuleFor(t => t.PacienteId).NotEmpty();
        RuleFor(t => t.UnidadeAtendimentoId).NotEmpty().WithMessage("Escolha a unidade de atendimento (destino).");
        RuleFor(t => t.TipoTratamentoId).NotEmpty().WithMessage("Escolha o tipo de tratamento — é dele que vem o tempo médio.");
        RuleFor(t => t.Descricao).NotEmpty().MaximumLength(500);
        RuleFor(t => t.Agenda).NotNull().SetValidator(new AgendaValidator());
        RuleFor(t => t.Necessidades).NotNull().SetValidator(new NecessidadesValidator());
        RuleFor(t => t.Acompanhantes).NotNull().SetValidator(new RegraAcompanhantesValidator());
    }
}

public sealed class AtualizarTratamentoValidator : AbstractValidator<AtualizarTratamentoRequest>
{
    public AtualizarTratamentoValidator()
    {
        RuleFor(t => t.UnidadeAtendimentoId).NotEmpty().WithMessage("Escolha a unidade de atendimento (destino).");
        RuleFor(t => t.TipoTratamentoId).NotEmpty().WithMessage("Escolha o tipo de tratamento — é dele que vem o tempo médio.");
        RuleFor(t => t.Descricao).NotEmpty().MaximumLength(500);
        RuleFor(t => t.Necessidades).NotNull().SetValidator(new NecessidadesValidator());
        RuleFor(t => t.Acompanhantes).NotNull().SetValidator(new RegraAcompanhantesValidator());
    }
}

/// <summary>Vale para o cadastro, para a troca de agenda e para a prévia.</summary>
public sealed class AgendaValidator : AbstractValidator<AgendaRequest>
{
    public AgendaValidator()
    {
        RuleFor(a => a.DataInicio).NotEmpty().WithMessage("Informe a data de início.");
        RuleFor(a => a.DiasSemanaMascara)
            .Must(AgendaDeSessoes.MascaraValida)
            .WithMessage("Marque ao menos um dia da semana.");
        When(a => !a.Continuo, () =>
            RuleFor(a => a.QuantidadeSessoes)
                .NotNull().WithMessage("Informe o número de sessões ou marque contínuo.")
                .InclusiveBetween(1, AgendaDeSessoes.LimiteDeSessoes)
                .WithMessage($"O número de sessões vai de 1 a {AgendaDeSessoes.LimiteDeSessoes}."));
    }
}

public sealed class NecessidadesValidator : AbstractValidator<NecessidadesRequest>
{
    public NecessidadesValidator()
    {
        RuleFor(n => n.Mobilidade).IsInEnum();
        RuleFor(n => n.AjudaDescricao).MaximumLength(500);
        When(n => n.NecessitaAjuda, () =>
            RuleFor(n => n.AjudaDescricao)
                .NotEmpty()
                .WithMessage("Descreva a ajuda de que o paciente precisa."));
    }
}

public sealed class RegraAcompanhantesValidator : AbstractValidator<RegraAcompanhantesRequest>
{
    public RegraAcompanhantesValidator()
    {
        RuleFor(r => r.Quantidade).InclusiveBetween(1, 2)
            .WithMessage("O atendimento permite 1 acompanhante, ou 2 com liberação.");
        RuleFor(r => r.JustificativaSegundo).MaximumLength(500);
        When(r => r.Quantidade == 2, () =>
            RuleFor(r => r.JustificativaSegundo)
                .NotEmpty()
                .WithMessage("O segundo acompanhante precisa de liberação: escreva a justificativa."));
    }
}

public sealed class DefinirAcompanhantesSessaoValidator : AbstractValidator<DefinirAcompanhantesSessaoRequest>
{
    public DefinirAcompanhantesSessaoValidator()
    {
        RuleFor(r => r.AcompanhanteIds).NotNull();
        RuleFor(r => r.AcompanhanteIds.Count).LessThanOrEqualTo(2)
            .When(r => r.AcompanhanteIds is not null)
            .WithMessage("No máximo 2 acompanhantes por viagem.");
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
        RuleFor(s => s.MotivoNaoRealizacao).MaximumLength(500);
        RuleFor(s => s.AcompanhanteIds!.Count).LessThanOrEqualTo(2)
            .When(s => s.AcompanhanteIds is not null)
            .WithMessage("No máximo 2 acompanhantes por viagem.");
        When(s => !s.Realizada, () =>
            RuleFor(s => s.MotivoNaoRealizacao)
                .NotEmpty()
                .WithMessage("Informe o motivo da não realização."));
    }
}
