using SMSMarica.Core.Integracoes.SisregWeb.Credencial.Dtos;

namespace SMSMarica.Core.Integracoes.SisregWeb.Credencial;

/// <summary>
/// Credencial de operador do SISREG da <b>unidade selecionada</b> (header X-Unidade-Id).
///
/// <para>Todos os métodos exigem UMA unidade selecionada — na visão "todas as unidades" não há
/// como decidir contra qual operador autenticar, então o fluxo é recusado.</para>
/// </summary>
public interface ISisregCredencialUnidadeService
{
    /// <summary>Estado da credencial da unidade selecionada (sem revelar a senha).</summary>
    Task<SisregCredencialUnidadeDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava usuário/senha <b>somente</b> se o SISREG aceitar a credencial e a unidade da sessão
    /// conferir com a unidade selecionada.
    /// </summary>
    Task<SisregAutenticacaoResultadoDto> SalvarAsync(
        SalvarSisregCredencialUnidadeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reautentica a credencial já gravada e revalida o vínculo com a unidade.</summary>
    Task<SisregAutenticacaoResultadoDto> TestarAsync(CancellationToken cancellationToken = default);

    /// <summary>Remove a credencial da unidade (volta a usar a credencial global).</summary>
    Task RemoverAsync(CancellationToken cancellationToken = default);
}
