using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Veiculos.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Veiculos;

[Mapper]
internal static partial class VeiculosMapper
{
    public static VeiculoDto ParaDto(Veiculo v) => new(
        v.Id, v.Placa, v.Modelo, v.Ativo, v.CriadoEm,
        [.. v.Fileiras
            .OrderBy(f => f.Ordem)
            .Select(f => new FileiraDto(
                f.Id, f.Ordem, f.QuantidadeAssentos,
                [.. f.Assentos
                    .OrderBy(a => a.Numero)
                    .Select(a => new AssentoDto(a.Id, a.Numero, a.Tipo))]))]);

    public static VeiculoListItemDto ParaListItem(Veiculo v) =>
        new(v.Id, v.Placa, v.Modelo, v.Ativo);

    public static FileiraDto ParaDto(Fileira f) => new(
        f.Id, f.Ordem, f.QuantidadeAssentos,
        [.. f.Assentos
            .OrderBy(a => a.Numero)
            .Select(a => new AssentoDto(a.Id, a.Numero, a.Tipo))]);
}
