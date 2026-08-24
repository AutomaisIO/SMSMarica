using SMSMais.Core.Inteligencia.Dtos;

namespace SMSMais.Core.Inteligencia.Configuracao;

/// <summary>Configuração global do módulo IA (token e modelo do provedor; rótulos genéricos na UI).</summary>
public interface IIaConfiguracaoService
{
    Task<ConfiguracaoDto> ObterAsync(CancellationToken cancellationToken = default);
    Task AtualizarAsync(AtualizarConfiguracaoRequest request, CancellationToken cancellationToken = default);
}

/// <summary>CRUD das bases de dados (fontes) e teste de conexão.</summary>
public interface IIaFonteService
{
    Task<IReadOnlyList<FonteDetalheDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarFonteRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarFonteRequest request, CancellationToken cancellationToken = default);
    Task RemoverAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TestarConexaoResultado> TestarConexaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gera/rotaciona o token de conexão do agente de uma base via agente (mostra uma vez).</summary>
    Task<TokenAgenteGerado> GerarTokenAgenteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisiona (idempotente) uma base via agente pelo slug: cria a base se não existir e
    /// (re)gera o token, devolvendo-o em claro uma vez. É o que o próprio agente chama no primeiro
    /// run, autenticado com o login do admin, para se auto-configurar. Ver ADR-0023.
    /// </summary>
    Task<TokenAgenteGerado> ProvisionarAgenteAsync(
        string slug, string? nome, CancellationToken cancellationToken = default);
}
