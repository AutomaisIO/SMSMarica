using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos.Dtos;

public sealed record CadastrarTratamentoRequest(
    Guid PacienteId,
    Guid UnidadeId,
    string Descricao,
    CadastrarPeriodicidadeRequest Periodicidade);

public sealed record CadastrarPeriodicidadeRequest(
    TipoPeriodicidade Tipo,
    int? IntervaloDias,
    int? DiasSemanaMascara,
    DateOnly DataInicio,
    int QuantidadeSessoes);
