using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos.Dtos;

public sealed record CadastrarTratamentoRequest(
    Guid PacienteId,
    Guid UnidadeId,
    Guid? TipoTratamentoId,
    string Descricao,
    string? CodigoSusLiberacao,
    string? Observacoes,
    TimeOnly? HoraPrevistaBusca,
    CadastrarPeriodicidadeRequest Periodicidade,
    /// <summary>
    /// Datas reais a gravar. Normalmente são geradas pelo algoritmo de expansão
    /// (preview no front) e podem ter sido editadas pelo operador antes de salvar.
    /// </summary>
    IReadOnlyList<DateOnly> Datas);

public sealed record CadastrarPeriodicidadeRequest(
    TipoPeriodicidade Tipo,
    int? IntervaloDias,
    int? DiasSemanaMascara,
    DateOnly DataInicio,
    int QuantidadeSessoes);

/// <summary>
/// Pedido para expandir uma periodicidade e retornar as datas resultantes —
/// usado na prévia do cadastro (antes de persistir). Mesma regra do servidor.
/// </summary>
public sealed record ExpandirPeriodicidadeRequest(
    TipoPeriodicidade Tipo,
    int? IntervaloDias,
    int? DiasSemanaMascara,
    DateOnly DataInicio,
    int QuantidadeSessoes);
