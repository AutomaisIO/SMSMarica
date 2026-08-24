using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Data.Entities;

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
        e.Ativo,
        e.CriadoEm);

    public static EquipamentoListItemDto ParaListItem(Equipamento e) => new(
        e.Id,
        e.Nome,
        e.UnidadeId,
        e.Unidade?.Nome ?? string.Empty,
        e.ModalidadeDicom,
        e.IdentificadorDicom,
        e.Ativo);
}
