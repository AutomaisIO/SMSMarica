using SMSMais.Core.Integracoes.Sisreg.Dtos;

namespace SMSMais.Core.Integracoes.Sisreg.Configuracao;

/// <summary>
/// Gerencia a linha única de configuração da integração SISREG. Senha e token são
/// write-only (a API só sinaliza se estão definidos). Ver ADR-0012.
/// </summary>
public interface ISisregConfiguracaoService
{
    /// <summary>Configuração mascarada para a tela (sem senha/token).</summary>
    Task<SisregConfiguracaoDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Atualiza a configuração; senha/token vazios mantêm o atual.</summary>
    Task AtualizarAsync(AtualizarSisregConfiguracaoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Contexto resolvido (segredos revelados) para o cliente HTTP. Lança
    /// <see cref="Common.Excecoes.ValidacaoException"/> se a integração não estiver
    /// configurada/ativa — usado para falhar de forma tratada antes da homologação.
    /// </summary>
    Task<SisregContexto> ObterContextoAsync(CancellationToken cancellationToken = default);
}
