namespace SMSMais.Core.Cidadao.Entrada;

/// <summary>
/// A configuração do app do cidadão por canal (ADR-0078). Ler e alterar é só de admin global
/// (<c>usuario.acesso_global</c>), conferido aqui a cada chamada — sem ele,
/// <see cref="UnauthorizedAccessException"/> (403).
/// </summary>
public interface IConfiguracaoAppCidadaoService
{
    Task<ConfiguracaoAppCidadaoDto> ObterAsync(CancellationToken ct = default);

    /// <summary>Grava e registra o ajuste na trilha de auditoria (de → para). Sem mudança, não grava nada.</summary>
    Task<ConfiguracaoAppCidadaoDto> AtualizarAsync(AtualizarConfiguracaoAppCidadaoRequest request, CancellationToken ct = default);

    /// <summary>Para a entrada (anônima): o canal consulta o e-SUS? Sem checagem de usuário.</summary>
    Task<bool> ConsultarEsusNoCanalAsync(string canal, CancellationToken ct = default);
}
