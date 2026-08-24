using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Veiculos.Dtos;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Veiculos;

[Mapper]
internal static partial class VeiculosMapper
{
    public static VeiculoDto ParaDto(Veiculo v) => new(
        v.Id, v.Placa, v.Modelo, v.Fabricante, v.Cor, v.Tipo, v.Ativo, v.CriadoEm,
        [.. v.Fileiras
            .OrderBy(f => f.Ordem)
            .Select(f => new FileiraDto(
                f.Id, f.Ordem, f.QuantidadeAssentos,
                [.. f.Assentos
                    .Where(a => !a.Excluido)
                    .OrderBy(a => a.Numero)
                    .Select(a => new AssentoDto(a.Id, a.Numero, a.Tipo, a.Bloqueado))]))]);

    public static VeiculoListItemDto ParaListItem(Veiculo v) =>
        new(v.Id, v.Placa, v.Modelo, v.Fabricante, v.Cor, v.Tipo, v.Ativo);

    public static FileiraDto ParaDto(Fileira f) => new(
        f.Id, f.Ordem, f.QuantidadeAssentos,
        [.. f.Assentos
            .Where(a => !a.Excluido)
            .OrderBy(a => a.Numero)
            .Select(a => new AssentoDto(a.Id, a.Numero, a.Tipo, a.Bloqueado))]);
}
