using SMSMarica.Core.Tickets.Dtos;

namespace SMSMarica.Core.Tickets;

public interface ITicketService
{
    // ---- Self-service (qualquer usuário autenticado) ----

    /// <summary>Lista os tickets que o usuário atual pode ver (conforme a visibilidade configurada).</summary>
    Task<IReadOnlyList<TicketListItemDto>> ListarVisiveisAsync(bool incluirArquivados, CancellationToken ct = default);

    /// <summary>Detalhe de um ticket visível ao usuário atual (comentários internos são omitidos).</summary>
    Task<TicketDto> ObterAsync(Guid id, CancellationToken ct = default);

    Task<Guid> AbrirAsync(AbrirTicketRequest request, CancellationToken ct = default);

    /// <summary>Comenta no próprio ticket (usuário comum) — não pode marcar como interno.</summary>
    Task ComentarAsync(Guid id, ComentarTicketRequest request, CancellationToken ct = default);

    /// <summary>Arquiva/desarquiva do lado do autor.</summary>
    Task ArquivarComoAutorAsync(Guid id, bool arquivar, CancellationToken ct = default);

    Task<TicketConfiguracaoDto> ObterConfiguracaoAsync(CancellationToken ct = default);

    /// <summary>Autor reconhece a resposta (baixa a "bandeira") sem precisar abrir o ticket.</summary>
    Task ReconhecerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Resumo do autor: quantas respostas ainda não reconhecidas (badge/notificação).</summary>
    Task<TicketResumoAutorDto> ObterResumoAutorAsync(CancellationToken ct = default);

    // ---- Gestão (exige módulo Ticket) ----

    Task<IReadOnlyList<TicketListItemDto>> ListarTodosAsync(bool incluirArquivados, CancellationToken ct = default);

    /// <summary>Resumo da gestão (badge do menu + cabeçalho): novos, abertos, em análise.</summary>
    Task<TicketResumoGestaoDto> ObterResumoGestaoAsync(CancellationToken ct = default);

    /// <summary>Detalhe de qualquer ticket, incluindo comentários internos.</summary>
    Task<TicketDto> ObterGestaoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Contexto enxuto (número/título/tipo/status) por número — alimenta a faixa do Agente IA.</summary>
    Task<TicketContextoDto> ObterContextoPorNumeroAsync(int numero, CancellationToken ct = default);

    /// <summary>Comenta em qualquer ticket; pode marcar como nota interna.</summary>
    Task ComentarGestaoAsync(Guid id, ComentarTicketRequest request, CancellationToken ct = default);

    /// <summary>Triagem: muda status/prioridade e/ou registra o retorno final.</summary>
    Task AtualizarGestaoAsync(Guid id, AtualizarTicketGestaoRequest request, CancellationToken ct = default);

    /// <summary>Marca que o ticket foi encaminhado ao Agente IA (marca "Enviado à IA" na lista).</summary>
    Task MarcarEnviadoIaAsync(Guid id, CancellationToken ct = default);

    Task ArquivarComoAdminAsync(Guid id, bool arquivar, CancellationToken ct = default);

    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    Task AtualizarVisibilidadeAsync(AtualizarVisibilidadeRequest request, CancellationToken ct = default);
}
