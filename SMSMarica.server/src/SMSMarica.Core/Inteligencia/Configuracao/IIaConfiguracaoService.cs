using SMSMarica.Core.Inteligencia.Dtos;

namespace SMSMarica.Core.Inteligencia.Configuracao;

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
}
