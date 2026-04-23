using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos.Dtos;

public sealed record TratamentoDto(
    Guid Id,
    Guid PacienteId,
    Guid UnidadeId,
    string Descricao,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? EncerradoEm,
    PeriodicidadeDto? Periodicidade);

public sealed record TratamentoListItemDto(
    Guid Id,
    Guid PacienteId,
    Guid UnidadeId,
    string Descricao,
    bool Ativo);

public sealed record PeriodicidadeDto(
    Guid Id,
    TipoPeriodicidade Tipo,
    int? IntervaloDias,
    int? DiasSemanaMascara,
    DateOnly DataInicio,
    int QuantidadeSessoes);
