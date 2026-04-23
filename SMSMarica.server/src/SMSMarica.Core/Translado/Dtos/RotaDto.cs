using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Translado.Dtos;

public sealed record RotaDiariaDto(
    Guid Id,
    DateOnly Data,
    Guid VeiculoId,
    Guid MotoristaId,
    StatusRota Status,
    DateTime CriadoEm,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm);

public sealed record RotaDiariaListItemDto(
    Guid Id,
    DateOnly Data,
    Guid VeiculoId,
    Guid MotoristaId,
    StatusRota Status);
