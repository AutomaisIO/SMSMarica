using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos.Dtos;

public sealed record EquipamentoDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string UnidadeNome,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    int DescricaoMaxCaracteres,
    bool Ativo,
    DateTime CriadoEm,
    // Código do procedimento na worklist (ADR-0074), em (0008,0100): em que sequência(s) vai e com
    // que esquema. As duas falsas = não envia (padrão). O esquema vazio sai como o padrão (99SMS).
    bool CodigoNoProcedimentoSolicitado = false,
    bool CodigoNoProtocoloAgendado = false,
    string? CodigoProcedimentoEsquema = null,
    // Campos opcionais (adendo do ADR-0074), tag no formato "0008,0090". Código: null = o padrão,
    // (0008,0100) dentro da sequência. Médico e unidade: null = não envia.
    string? CodigoProcedimentoTag = null,
    string? MedicoSolicitanteTag = null,
    string? UnidadeTag = null,
    OrigemUnidadeWorklist UnidadeOrigem = OrigemUnidadeWorklist.Executante,
    // O que acontece com o exame depois que chega (ADR-0075).
    OrigemLaudo Laudo = OrigemLaudo.Plataforma,
    LiberacaoExamePaciente LiberacaoPaciente = LiberacaoExamePaciente.ComLaudoAssinado,
    bool UsaAnamnese = true);

public sealed record EquipamentoListItemDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string UnidadeNome,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    int DescricaoMaxCaracteres,
    bool Ativo,
    bool CodigoNoProcedimentoSolicitado = false,
    bool CodigoNoProtocoloAgendado = false,
    string? CodigoProcedimentoEsquema = null,
    // Campos opcionais (adendo do ADR-0074), tag no formato "0008,0090". Código: null = o padrão,
    // (0008,0100) dentro da sequência. Médico e unidade: null = não envia.
    string? CodigoProcedimentoTag = null,
    string? MedicoSolicitanteTag = null,
    string? UnidadeTag = null,
    OrigemUnidadeWorklist UnidadeOrigem = OrigemUnidadeWorklist.Executante,
    // O que acontece com o exame depois que chega (ADR-0075).
    OrigemLaudo Laudo = OrigemLaudo.Plataforma,
    LiberacaoExamePaciente LiberacaoPaciente = LiberacaoExamePaciente.ComLaudoAssinado,
    bool UsaAnamnese = true);

public sealed record CadastrarEquipamentoRequest(
    string Nome,
    Guid UnidadeId,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    /// <summary>Quantos caracteres da descrição este aparelho aguenta na worklist. Omitido = 64,
    /// o teto do DICOM. Só baixar quando um console concreto falhar (ver o Fuji FDR-3000AWS).</summary>
    int DescricaoMaxCaracteres = Equipamento.DescricaoMaxPadrao,
    /// <summary>Código do procedimento (0008,0100) na Requested Procedure Code Sequence (0032,1064)
    /// e/ou na Scheduled Protocol Code Sequence (0040,0008). Omitido = não envia.</summary>
    bool CodigoNoProcedimentoSolicitado = false,
    bool CodigoNoProtocoloAgendado = false,
    string? CodigoProcedimentoEsquema = null,
    /// <summary>Tag do código quando o aparelho não o lê numa sequência. Vazio ou (0008,0100) = o
    /// padrão (na sequência); outra tag = o código vai como texto nela.</summary>
    string? CodigoProcedimentoTag = null,
    /// <summary>Médico solicitante na worklist. Tag vazia = (0008,0090).</summary>
    bool EnviarMedicoSolicitante = false,
    string? MedicoSolicitanteTag = null,
    /// <summary>Nome da unidade ("clínica"), da executante ou da solicitante. Tag vazia = (0008,0080)
    /// para a executante, (0032,1033) para a solicitante.</summary>
    bool EnviarUnidade = false,
    OrigemUnidadeWorklist UnidadeOrigem = OrigemUnidadeWorklist.Executante,
    string? UnidadeTag = null,
    /// <summary>Onde nasce o laudo (ADR-0075). Omitido = o padrão da modalidade (ultrassom: fora;
    /// o resto: na plataforma) — um ultrassom criado por script não pode nascer retendo exame.</summary>
    OrigemLaudo? Laudo = null,
    /// <summary>Quando o paciente recebe o exame. Omitido = o padrão da modalidade; com o laudo
    /// informado como fora ou junto, "assim que chegar" (só com o laudo exige o laudo aqui).</summary>
    LiberacaoExamePaciente? LiberacaoPaciente = null,
    bool UsaAnamnese = true);

/// <summary>
/// Atualização do equipamento. Os campos da worklist são <b>opcionais e "omitido = mantém"</b>: até
/// 16/10/2026 o painel não mandava o limite de descrição, e cada edição gravava 64 por cima do 16
/// do Fuji. Ausente nunca mais desfaz configuração de aparelho.
/// </summary>
public sealed record AtualizarEquipamentoRequest(
    string Nome,
    Guid UnidadeId,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    bool Ativo,
    int? DescricaoMaxCaracteres = null,
    /// <summary>Onde vai o código do procedimento. As duas null = mantém o que está gravado (e o
    /// esquema junto); informadas = grava destinos e esquema (esquema vazio = o padrão).</summary>
    bool? CodigoNoProcedimentoSolicitado = null,
    bool? CodigoNoProtocoloAgendado = null,
    string? CodigoProcedimentoEsquema = null,
    /// <summary>Null = mantém; vazio ou (0008,0100) = o padrão (na sequência); outra = texto nela.</summary>
    string? CodigoProcedimentoTag = null,
    /// <summary>Null = mantém. Ligado com tag vazia = (0008,0090).</summary>
    bool? EnviarMedicoSolicitante = null,
    string? MedicoSolicitanteTag = null,
    /// <summary>Null = mantém. Ligado com tag vazia = o padrão da origem.</summary>
    bool? EnviarUnidade = null,
    OrigemUnidadeWorklist? UnidadeOrigem = null,
    string? UnidadeTag = null,
    /// <summary>Null = mantém (ADR-0075). A combinação final é conferida contra o que está gravado:
    /// "só com o laudo assinado" exige o laudo na plataforma.</summary>
    OrigemLaudo? Laudo = null,
    LiberacaoExamePaciente? LiberacaoPaciente = null,
    bool? UsaAnamnese = null);
