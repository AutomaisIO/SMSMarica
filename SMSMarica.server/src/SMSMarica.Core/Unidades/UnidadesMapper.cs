using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Unidades.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Unidades;

internal static class UnidadesMapper
{
    public static UnidadeDto ParaDto(Unidade u) => new(
        u.Id,
        u.Nome,
        u.Endereco is null ? null : EnderecoDto.ParaDto(u.Endereco),
        u.Telefone,
        u.Gps?.Latitude,
        u.Gps?.Longitude,
        u.Ativo,
        u.CriadoEm);

    public static UnidadeListItemDto ParaListItem(Unidade u) => new(
        u.Id,
        u.Nome,
        u.Endereco?.Cidade,
        u.Endereco?.Uf,
        u.Ativo);
}
