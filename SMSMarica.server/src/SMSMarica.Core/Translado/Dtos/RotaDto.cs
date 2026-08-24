using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Translado.Dtos;

public sealed record RotaDiariaDto(
    Guid Id,
    DateOnly Data,
    Guid VeiculoId,
    string VeiculoPlaca,
    string VeiculoModelo,
    Guid MotoristaId,
    string MotoristaNome,
    StatusRota Status,
    DateTime CriadoEm,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm,
    IReadOnlyList<AlocacaoDto> Alocacoes);

public sealed record RotaDiariaListItemDto(
    Guid Id,
    DateOnly Data,
    Guid VeiculoId,
    string VeiculoPlaca,
    Guid MotoristaId,
    string MotoristaNome,
    StatusRota Status,
    int TotalAlocacoes);
