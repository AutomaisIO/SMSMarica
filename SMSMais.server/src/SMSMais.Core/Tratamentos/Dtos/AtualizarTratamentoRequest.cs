namespace SMSMais.Core.Tratamentos.Dtos;

/// <summary>
/// Ajustes no atendimento cadastrado: dados, condição do paciente e regra de acompanhantes. A
/// agenda muda à parte (<c>PUT /tratamentos/{id}/agenda</c>), que refaz as sessões futuras.
/// </summary>
public sealed record AtualizarTratamentoRequest(
    string Descricao,
    Guid UnidadeAtendimentoId,
    Guid? TipoTratamentoId,
    string? Observacoes,
    NecessidadesRequest Necessidades,
    RegraAcompanhantesRequest Acompanhantes);

/// <summary>Quem vai acompanhar o paciente numa viagem (ids da lista de acompanhantes dele).</summary>
public sealed record DefinirAcompanhantesSessaoRequest(IReadOnlyList<Guid> AcompanhanteIds);

/// <summary>
/// Alterações permitidas em uma sessão: data prevista, horários previstos, observações.
/// </summary>
public sealed record AtualizarSessaoRequest(
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    TimeOnly? HoraPrevistaRetorno,
    string? Observacoes);

/// <summary>
/// Adiciona uma nova sessão (data) a um atendimento existente.
/// </summary>
public sealed record AdicionarSessaoRequest(
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    TimeOnly? HoraPrevistaRetorno);

/// <summary>
/// Confirmação de realização de uma sessão. Campos opcionais suportam preenchimento incremental.
/// Enviar realizada=false marca NaoRealizada. <paramref name="AcompanhanteIds"/> registra quem foi
/// de fato (nulo = mantém a escolha feita antes da viagem).
/// </summary>
public sealed record ConfirmarSessaoRequest(
    bool Realizada,
    IReadOnlyList<Guid>? AcompanhanteIds,
    Guid? MotoristaIdaId,
    Guid? VeiculoIdaId,
    TimeOnly? HoraSaidaResidencia,
    TimeOnly? HoraChegadaUnidade,
    Guid? MotoristaVoltaId,
    Guid? VeiculoVoltaId,
    TimeOnly? HoraSaidaUnidade,
    TimeOnly? HoraChegadaResidencia,
    string? MotivoNaoRealizacao,
    string? Observacoes);
