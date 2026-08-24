using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Unidades.Dtos;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Unidades;

internal static class UnidadesMapper
{
    public static UnidadeDto ParaDto(Unidade u) => new(
        u.Id,
        u.Nome,
        u.Cnes,
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
