namespace SMSMarica.Core.Inteligencia.Governanca;

/// <summary>
/// Governança do aprendizado do módulo IA: revisar e desativar instruções aprendidas
/// (manuais e auto-correções) e auditar o histórico de correções.
/// </summary>
public interface IIaGovernancaService
{
    /// <summary>Lista os aprendizados ativos (não excluídos), opcionalmente filtrando por fonte.</summary>
    Task<IReadOnlyList<AprendizadoDto>> ListarAprendizadosAsync(
        Guid? fonteId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Desativa um aprendizado (Ativo=false + soft-delete).</summary>
    Task DesativarAprendizadoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista o histórico de correções, opcionalmente filtrando por fonte.</summary>
    Task<IReadOnlyList<CorrecaoDto>> ListarCorrecoesAsync(
        Guid? fonteId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Avaliações 👎 da Consulta Inteligente (por padrão só as pendentes de tratamento).</summary>
    Task<IReadOnlyList<FeedbackDto>> ListarFeedbacksAsync(
        bool apenasPendentes = true, CancellationToken cancellationToken = default);

    /// <summary>Marca uma avaliação como tratada (virou conhecimento) ou descartada.</summary>
    Task TratarFeedbackAsync(
        Guid id, TratarFeedbackRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Aprendizado incremental de uma fonte (hint, exemplo, glossário, correção).</summary>
public sealed record AprendizadoDto(
    Guid Id,
    Guid FonteId,
    string FonteNome,
    string Tipo,
    string Origem,
    string Conteudo,
    bool Ativo,
    DateTime CriadoEm);

/// <summary>Avaliação de uma resposta da Consulta Inteligente (👎 gera melhoria).</summary>
public sealed record FeedbackDto(
    Guid Id,
    Guid FonteId,
    string FonteNome,
    string? Familia,
    string Pergunta,
    string? Resposta,
    string? Comentario,
    string Status,
    string? Resolucao,
    DateTime CriadoEm);

/// <summary>Tratamento de uma avaliação: virou conhecimento (default) ou foi descartada.</summary>
public sealed record TratarFeedbackRequest(bool Descartar, string? Resolucao);

/// <summary>Entrada do histórico de correções automáticas de SQL.</summary>
public sealed record CorrecaoDto(
    Guid Id,
    Guid ConsultaId,
    Guid? AprendizadoId,
    string Pergunta,
    string ErroOriginal,
    string? SqlAntes,
    string? SqlDepois,
    string? InstrucaoGerada,
    DateTime CriadoEm,
    DateTime? RevisadoEm,
    DateTime? RemovidoEm);
