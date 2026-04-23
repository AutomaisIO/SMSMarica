using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Translado.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Translado;

[Mapper]
internal static partial class TransladoMapper
{
    public static RotaDiariaDto ParaDto(RotaDiaria r) => new(
        r.Id, r.Data, r.VeiculoId, r.MotoristaId, r.Status,
        r.CriadoEm, r.IniciadaEm, r.ConcluidaEm);

    public static RotaDiariaListItemDto ParaListItem(RotaDiaria r) =>
        new(r.Id, r.Data, r.VeiculoId, r.MotoristaId, r.Status);
}
