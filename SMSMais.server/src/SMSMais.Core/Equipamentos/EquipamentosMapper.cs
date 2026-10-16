using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos;

internal static class EquipamentosMapper
{
    public static EquipamentoDto ParaDto(Equipamento e) => new(
        e.Id,
        e.Nome,
        e.UnidadeId,
        e.Unidade?.Nome ?? string.Empty,
        e.ModalidadeDicom,
        e.IdentificadorDicom,
        e.DescricaoMaxCaracteres,
        e.Ativo,
        e.CriadoEm,
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProcedimentoSolicitado),
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProtocoloAgendado),
        e.CodigoProcedimentoEsquema);

    public static EquipamentoListItemDto ParaListItem(Equipamento e) => new(
        e.Id,
        e.Nome,
        e.UnidadeId,
        e.Unidade?.Nome ?? string.Empty,
        e.ModalidadeDicom,
        e.IdentificadorDicom,
        e.DescricaoMaxCaracteres,
        e.Ativo,
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProcedimentoSolicitado),
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProtocoloAgendado),
        e.CodigoProcedimentoEsquema);

    /// <summary>As duas caixas da tela → o campo combinável gravado (ADR-0074).</summary>
    public static DestinoCodigoProcedimento Destinos(bool noProcedimentoSolicitado, bool noProtocoloAgendado) =>
        (noProcedimentoSolicitado ? DestinoCodigoProcedimento.ProcedimentoSolicitado : DestinoCodigoProcedimento.Nenhum)
        | (noProtocoloAgendado ? DestinoCodigoProcedimento.ProtocoloAgendado : DestinoCodigoProcedimento.Nenhum);
}
