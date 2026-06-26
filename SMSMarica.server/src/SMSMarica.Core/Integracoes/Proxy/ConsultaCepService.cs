using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Dtos;
using SMSMarica.Core.Integracoes.Proxy.Configuracao;

namespace SMSMarica.Core.Integracoes.Proxy;

/// <summary>Consulta endereço por CEP com fallback entre os motores ativos.</summary>
public interface IConsultaCepService
{
    Task<HubCepRespostaDto> ConsultarCepAsync(string cep, CancellationToken cancellationToken = default);

    /// <summary>Testa um motor específico (sem fallback), com a config salva dele.</summary>
    Task<ProxyTesteCepResultado> TestarMotorAsync(string motor, string cep, CancellationToken cancellationToken = default);
}

public sealed class ConsultaCepService(
    IEnumerable<IMotorCep> motores,
    IProxyMotorConfiguracaoService configuracao,
    ILogger<ConsultaCepService> logger) : IConsultaCepService
{
    private readonly IReadOnlyDictionary<string, IMotorCep> _motores =
        motores.ToDictionary(m => m.Motor, StringComparer.OrdinalIgnoreCase);

    public async Task<HubCepRespostaDto> ConsultarCepAsync(string cep, CancellationToken cancellationToken = default)
    {
        var cepNormalizado = new string([.. (cep ?? string.Empty).Where(char.IsDigit)]);
        if (cepNormalizado.Length != 8)
        {
            throw new ValidacaoException("proxy.cep_invalido", "CEP deve ter 8 dígitos.");
        }

        var ativos = await configuracao.ObterMotoresAtivosAsync(ServicosProxy.Cep, cancellationToken);
        var cadeia = ativos
            .Where(a => _motores.ContainsKey(a.Motor))
            .Select(a => (a.Motor, a.Cfg))
            .ToList();

        return await ProxyExecutor.ExecutarAsync(
            ServicosProxy.Cep,
            cadeia,
            (motor, cfg, ct) => _motores[motor].ConsultarAsync(cepNormalizado, cfg, ct),
            logger,
            cancellationToken);
    }

    public async Task<ProxyTesteCepResultado> TestarMotorAsync(
        string motor, string cep, CancellationToken cancellationToken = default)
    {
        var cepNormalizado = new string([.. (cep ?? string.Empty).Where(char.IsDigit)]);
        if (cepNormalizado.Length != 8)
        {
            return new ProxyTesteCepResultado(false, "CEP deve ter 8 dígitos.", null, 0);
        }
        if (!_motores.TryGetValue(motor, out var impl))
        {
            return new ProxyTesteCepResultado(false, $"Motor '{motor}' não suportado para CEP.", null, 0);
        }

        var cfg = await configuracao.ObterExecucaoAsync(ServicosProxy.Cep, motor, cancellationToken);
        var (ok, msg, resultado, ms) = await ProxyTeste.ExecutarAsync(
            cfg, ct => impl.ConsultarAsync(cepNormalizado, cfg, ct), cancellationToken);
        return new ProxyTesteCepResultado(ok, msg, resultado, ms);
    }
}
