using SMSMarica.Core.Inteligencia.Fontes.Agente;
using SMSMarica.Core.Inteligencia.Validacao;

namespace SMSMarica.Core.Inteligencia.Fontes;

/// <summary>
/// Fonte alcançada por agente proxy: o smsmarica não fala com o banco, fala com o agente que roda
/// no servidor de destino e disca para cá por WSS. O SQL passa pelo mesmo guard read-only antes de
/// sair; o agente também guarda do lado dele (defesa em profundidade). Ver ADR-0023.
/// </summary>
public sealed class ProxyAgenteFonte(
    IAgenteSqlRegistry registry,
    string agenteId,
    int commandTimeoutSegundos,
    int maxLinhas) : IFonteDados
{
    public async Task<ResultadoConsulta> ExecutarAsync(
        string sql, CancellationToken cancellationToken = default, int? maxLinhasOverride = null)
    {
        SqlReadOnlyGuard.GarantirLeitura(sql);
        var cap = maxLinhasOverride ?? maxLinhas;
        return await registry.ExecutarAsync(agenteId, sql, commandTimeoutSegundos, cap, cancellationToken);
    }

    public Task<bool> TestarConexaoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(registry.EstaConectado(agenteId));
}
