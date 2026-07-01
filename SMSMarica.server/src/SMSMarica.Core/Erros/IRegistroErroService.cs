using SMSMarica.Core.Erros.Dtos;

namespace SMSMarica.Core.Erros;

/// <summary>
/// Log de erros não tratados (500). O middleware chama <see cref="RegistrarAsync"/>
/// ao capturar uma exceção inesperada; a tela de diagnóstico usa a busca.
/// </summary>
public interface IRegistroErroService
{
    /// <summary>
    /// Persiste um erro e devolve o código de referência gerado (ex.: "ERRO-4F9C2A")
    /// para ser mostrado ao usuário. Best-effort: nunca deve derrubar o pipeline —
    /// o chamador trata falha de gravação.
    /// </summary>
    Task<string> RegistrarAsync(RegistrarErroDados dados, CancellationToken cancellationToken = default);

    Task<PaginaErrosDto> BuscarAsync(ErroFiltroDto filtro, CancellationToken cancellationToken = default);

    /// <summary>Detalhe completo (com stack trace) de um erro pelo código de referência.</summary>
    Task<RegistroErroDto?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}
