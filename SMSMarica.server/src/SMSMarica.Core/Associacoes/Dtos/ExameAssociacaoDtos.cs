using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes.Dtos;

/// <summary>Pedido de associação manual de um estudo do PACS a uma solicitação.</summary>
public sealed record AssociarExameRequest(
    string StudyInstanceUID,
    string AccessionNumber,
    string? AccessionNumberDicomOriginal = null);

/// <summary>
/// Vínculo resolvido de um estudo (explícito via tabela, ou implícito por
/// StudyInstanceUID de worklist). Usado na listagem e no preview.
/// </summary>
public sealed record ExameAssociacaoDto(
    string StudyInstanceUID,
    Guid SolicitacaoExameId,
    string AccessionNumber,
    Guid PacienteId,
    string? PacienteNome,
    bool Explicita,
    OrigemAssociacaoExame? Origem,
    PrioridadeSolicitacao Prioridade,
    bool TemAnamnese);

/// <summary>Vínculo mínimo (solicitação + paciente) usado pelo gate de laudar.</summary>
public sealed record VinculoExame(Guid SolicitacaoExameId, Guid PacienteId);

/// <summary>Contadores de uma passada de conciliação PACS→solicitações (lote).</summary>
public sealed record ConciliacaoLoteResultado(int Conciliadas, int JaConciliadas, int SemSolicitacao, int Falhas);

/// <summary>
/// Resultado de uma resincronização sob demanda (PACS-driven): varre os studies recentes
/// do PACS (pela data do EXAME) e concilia cada um com as solicitações. Nomes dos campos
/// mantidos por compatibilidade com o front: Candidatas/Varridas = studies varridos;
/// Associadas = conciliados nesta passada; SemExameNoPacs = studies órfãos (sem
/// solicitação aberta correspondente).
/// </summary>
public sealed record ResincronizacaoResultadoDto(
    int Candidatas,
    int Varridas,
    int Associadas,
    int SemExameNoPacs,
    int Falhas,
    bool LimiteAtingido);
