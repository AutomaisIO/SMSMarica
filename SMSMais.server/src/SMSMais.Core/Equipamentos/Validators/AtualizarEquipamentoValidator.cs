using FluentValidation;
using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos.Validators;

public sealed class AtualizarEquipamentoValidator : AbstractValidator<AtualizarEquipamentoRequest>
{
    public AtualizarEquipamentoValidator()
    {
        RuleFor(e => e.Nome).NotEmpty().MaximumLength(200);
        RuleFor(e => e.UnidadeId).NotEmpty();
        RuleFor(e => e.ModalidadeDicom).IsInEnum();
        RuleFor(e => e.DescricaoMaxCaracteres)
            .InclusiveBetween(4, Equipamento.DescricaoMaxPadrao)
            .WithMessage($"O limite de descrição vai de 4 a {Equipamento.DescricaoMaxPadrao} " +
                         "caracteres (64 é o teto do próprio DICOM).")
            .When(e => e.DescricaoMaxCaracteres.HasValue);

        RuleFor(e => e.IdentificadorDicom)
            .Must(ResolvedorEstacaoWorklist.AeTitleValido)
            .WithMessage("Identificador DICOM deve ser um AE Title válido: até 16 caracteres, sem espaços nem acentos.")
            .When(e => !string.IsNullOrWhiteSpace(e.IdentificadorDicom));

        RuleFor(e => e.CodigoProcedimentoEsquema)
            .Must(RegrasCodigoProcedimento.EsquemaValido)
            .WithMessage(RegrasCodigoProcedimento.MensagemEsquema);

        RuleFor(e => e.CodigoProcedimentoTag)
            .Must(t => RegrasCamposWorklist.ProblemaTagCodigo(t) is null)
            .WithMessage((_, t) => RegrasCamposWorklist.ProblemaTagCodigo(t));
        RuleFor(e => e.MedicoSolicitanteTag)
            .Must(t => RegrasCamposWorklist.ProblemaTagCampo(t) is null)
            .WithMessage((_, t) => RegrasCamposWorklist.ProblemaTagCampo(t))
            .When(e => e.EnviarMedicoSolicitante == true);
        RuleFor(e => e.UnidadeOrigem).IsInEnum();
        RuleFor(e => e.UnidadeTag)
            .Must(t => RegrasCamposWorklist.ProblemaTagCampo(t) is null)
            .WithMessage((_, t) => RegrasCamposWorklist.ProblemaTagCampo(t))
            .When(e => e.EnviarUnidade == true);
        RuleFor(e => e)
            .Must(e => RegrasCamposWorklist.Repetida(e.CodigoProcedimentoTag, e.EnviarMedicoSolicitante == true, e.MedicoSolicitanteTag, e.EnviarUnidade == true, e.UnidadeOrigem ?? OrigemUnidadeWorklist.Executante, e.UnidadeTag) is null)
            .WithMessage(e => RegrasCamposWorklist.Repetida(e.CodigoProcedimentoTag, e.EnviarMedicoSolicitante == true, e.MedicoSolicitanteTag, e.EnviarUnidade == true, e.UnidadeOrigem ?? OrigemUnidadeWorklist.Executante, e.UnidadeTag))
            .WithName("CamposWorklist");

        // Laudo e liberação ao paciente (ADR-0075). Com um dos dois omitido, quem confere a
        // combinação é o serviço, que conhece o valor gravado.
        RuleFor(e => e.Laudo).IsInEnum().When(e => e.Laudo.HasValue);
        RuleFor(e => e.LiberacaoPaciente).IsInEnum().When(e => e.LiberacaoPaciente.HasValue);
        RuleFor(e => e)
            .Must(e => EquipamentosMapper.CombinacaoLiberacaoValida(e.Laudo!.Value, e.LiberacaoPaciente!.Value))
            .WithMessage(EquipamentosMapper.MensagemCombinacaoLiberacao)
            .WithName("LiberacaoPaciente")
            .When(e => e.Laudo.HasValue && e.LiberacaoPaciente.HasValue);
    }
}
