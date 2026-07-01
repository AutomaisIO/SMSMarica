using SMSMarica.Core.Auditoria.Dtos;

namespace SMSMarica.Core.Auditoria;

/// <summary>
/// Trilha de auditoria de ações de usuário. Grava o rastro (append-only) e o
/// consulta. O "quem" é resolvido internamente via <c>IUsuarioAtualAccessor</c>
/// — o chamador só informa a ação e os valores.
/// </summary>
public interface IAuditoriaService
{
    /// <summary>
    /// Registra uma ação na trilha. Resolve o usuário atual (id + nome) sozinho;
    /// em contexto sem autenticação (job/seed) grava com usuário nulo, sem lançar.
    /// </summary>
    Task RegistrarAsync(
        string entidade,
        string entidadeId,
        string acao,
        string? valorAnterior,
        string? valorNovo,
        CancellationToken cancellationToken = default);

    /// <summary>Busca paginada da trilha, mais recentes primeiro.</summary>
    Task<PaginaAuditoriaDto> BuscarAsync(AuditoriaFiltroDto filtro, CancellationToken cancellationToken = default);
}
