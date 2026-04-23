using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Unidades.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Unidades;

[Mapper]
internal static partial class UnidadesMapper
{
    public static UnidadeDto ParaDto(Unidade u) => new(
        u.Id, u.Nome, u.Endereco, u.Telefone,
        u.Gps.Latitude, u.Gps.Longitude,
        u.Ativo, u.CriadoEm);

    public static UnidadeListItemDto ParaListItem(Unidade u) => new(u.Id, u.Nome, u.Ativo);
}
