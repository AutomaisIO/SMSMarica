using SMSMarica.Core.Translado.Dtos;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Translado;

internal static class TransladoMapper
{
    public static RotaDiariaDto ParaDto(RotaDiaria r, IReadOnlyList<AlocacaoDto> alocacoes) => new(
        r.Id,
        r.Data,
        r.VeiculoId,
        r.Veiculo?.Placa ?? string.Empty,
        r.Veiculo?.Modelo ?? string.Empty,
        r.MotoristaId,
        r.Motorista?.Usuario?.NomeCompleto ?? string.Empty,
        r.Status,
        r.CriadoEm,
        r.IniciadaEm,
        r.ConcluidaEm,
        alocacoes);

    public static RotaDiariaListItemDto ParaListItem(RotaDiaria r, int totalAlocacoes) => new(
        r.Id,
        r.Data,
        r.VeiculoId,
        r.Veiculo?.Placa ?? string.Empty,
        r.MotoristaId,
        r.Motorista?.Usuario?.NomeCompleto ?? string.Empty,
        r.Status,
        totalAlocacoes);
}
