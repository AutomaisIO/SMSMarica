namespace SMSMais.Core.Tratamentos.Dtos;

/// <summary>
/// Ajustes editáveis no tratamento cadastrado. Periodicidade é imutável;
/// para mudar cadência, edite as sessões individualmente (ou encerre e
/// recadastre).
/// </summary>
public sealed record AtualizarTratamentoRequest(
    string Descricao,
    Guid? TipoTratamentoId,
    string? CodigoSusLiberacao,
    string? Observacoes,
    TimeOnly? HoraPrevistaBusca);

/// <summary>
/// Alterações permitidas em uma sessão: data prevista, horários previstos,
/// status (ex: cancelar sessão futura).
/// </summary>
public sealed record AtualizarSessaoRequest(
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    TimeOnly? HoraPrevistaRetorno,
    string? Observacoes);

/// <summary>
/// Adiciona uma nova sessão (data) a um tratamento existente.
/// </summary>
public sealed record AdicionarSessaoRequest(
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    TimeOnly? HoraPrevistaRetorno);

/// <summary>
/// Confirmação de realização de uma sessão. Campos opcionais suportam
/// preenchimento incremental; obrigatório ao menos um dos horários para
/// virar status Realizada. Enviar realizada=false marca NaoRealizada.
/// </summary>
public sealed record ConfirmarSessaoRequest(
    bool Realizada,
    string? NomeAcompanhante,
    string? ParentescoAcompanhante,
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
