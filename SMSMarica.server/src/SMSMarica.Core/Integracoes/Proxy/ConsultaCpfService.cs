using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Dtos;
using SMSMarica.Core.Integracoes.Proxy.Configuracao;

namespace SMSMarica.Core.Integracoes.Proxy;

/// <summary>Consulta CPF na Receita com fallback entre os motores ativos.</summary>
public interface IConsultaCpfService
{
    Task<HubCpfRespostaDto> ConsultarCpfAsync(string cpf, DateOnly dataNascimento, CancellationToken cancellationToken = default);

    /// <summary>Testa um motor específico (sem fallback), com a config salva dele.</summary>
    Task<ProxyTesteCpfResultado> TestarMotorAsync(
        string motor, string cpf, DateOnly dataNascimento, CancellationToken cancellationToken = default);
}

public sealed class ConsultaCpfService(
    IEnumerable<IMotorCpf> motores,
    IProxyMotorConfiguracaoService configuracao,
    ILogger<ConsultaCpfService> logger) : IConsultaCpfService
{
    private readonly IReadOnlyDictionary<string, IMotorCpf> _motores =
        motores.ToDictionary(m => m.Motor, StringComparer.OrdinalIgnoreCase);

    public async Task<HubCpfRespostaDto> ConsultarCpfAsync(
        string cpf, DateOnly dataNascimento, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = new string([.. (cpf ?? string.Empty).Where(char.IsDigit)]);
        if (cpfNormalizado.Length != 11)
        {
            throw new ValidacaoException("proxy.cpf_invalido", "CPF deve ter 11 dígitos.");
        }

        var ativos = await configuracao.ObterMotoresAtivosAsync(ServicosProxy.Cpf, cancellationToken);
        var cadeia = ativos
            .Where(a => _motores.ContainsKey(a.Motor))
            .Select(a => (a.Motor, a.Cfg))
            .ToList();

        return await ProxyExecutor.ExecutarAsync(
            ServicosProxy.Cpf,
            cadeia,
            (motor, cfg, ct) => _motores[motor].ConsultarAsync(cpfNormalizado, dataNascimento, cfg, ct),
            logger,
            cancellationToken);
    }

    public async Task<ProxyTesteCpfResultado> TestarMotorAsync(
        string motor, string cpf, DateOnly dataNascimento, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = new string([.. (cpf ?? string.Empty).Where(char.IsDigit)]);
        if (cpfNormalizado.Length != 11)
        {
            return new ProxyTesteCpfResultado(false, "CPF deve ter 11 dígitos.", null, 0);
        }
        if (!_motores.TryGetValue(motor, out var impl))
        {
            return new ProxyTesteCpfResultado(false, $"Motor '{motor}' não suportado para CPF.", null, 0);
        }

        var cfg = await configuracao.ObterExecucaoAsync(ServicosProxy.Cpf, motor, cancellationToken);
        var (ok, msg, resultado, ms) = await ProxyTeste.ExecutarAsync(
            cfg, ct => impl.ConsultarAsync(cpfNormalizado, dataNascimento, cfg, ct), cancellationToken);
        return new ProxyTesteCpfResultado(ok, msg, resultado, ms);
    }
}
