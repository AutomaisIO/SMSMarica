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
    string? CodigoProcedimentoEsquema = null);

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
    string? CodigoProcedimentoEsquema = null);

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
    string? CodigoProcedimentoEsquema = null);

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
    string? CodigoProcedimentoEsquema = null);
