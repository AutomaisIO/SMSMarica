using SMSMarica.Core.Erros.Dtos;

namespace SMSMarica.Core.Erros;

/// <summary>
/// Log de erros não tratados (500). O middleware chama <see cref="RegistrarAsync"/>
/// ao capturar uma exceção inesperada; a tela de diagnóstico usa a busca.
/// </summary>
public interface IRegistroErroService
{
    /// <summary>
    /// Persiste um erro e devolve o código de referência (ex.: "ERRO-4F9C2A") para mostrar ao
    /// usuário. Se um erro idêntico (mesma assinatura) já estiver em aberto, REUSA o código e
    /// incrementa as ocorrências — <c>JaReportado=true</c>. Best-effort: nunca deve derrubar o
    /// pipeline — o chamador trata falha de gravação.
    /// </summary>
    Task<RegistroErroResultado> RegistrarAsync(RegistrarErroDados dados, CancellationToken cancellationToken = default);

    Task<PaginaErrosDto> BuscarAsync(ErroFiltroDto filtro, CancellationToken cancellationToken = default);

    /// <summary>Detalhe completo (com stack trace) de um erro pelo código de referência.</summary>
    Task<RegistroErroDto?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca o erro como resolvido (data + autor + nota). Reincidências posteriores geram um
    /// código novo. Devolve o registro atualizado; null se o código não existe.
    /// </summary>
    Task<RegistroErroDto?> ResolverAsync(string codigo, ResolverErroRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reabre um erro resolvido (limpa a marcação). Devolve o registro; null se não existe.</summary>
    Task<RegistroErroDto?> ReabrirAsync(string codigo, CancellationToken cancellationToken = default);
}
