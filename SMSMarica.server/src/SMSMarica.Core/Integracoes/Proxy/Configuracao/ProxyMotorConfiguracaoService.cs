using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Integracoes;

namespace SMSMarica.Core.Integracoes.Proxy.Configuracao;

/// <summary>
/// Store (uma linha por serviço+motor) das configurações de proxy. Token cifrado/decifrado
/// via <see cref="IProtetorSegredos"/> e write-only na API. Um motor que exige token e não
/// tem nenhum cadastrado fica fora da cadeia de fallback.
/// </summary>
public sealed class ProxyMotorConfiguracaoService(
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IUsuarioAtualAccessor usuarioAtual) : IProxyMotorConfiguracaoService
{
    private const int TimeoutPadrao = 10;
    private const int TentativasPadrao = 3;

    public async Task<IReadOnlyList<ProxyMotorDto>> ListarAsync(string servico, CancellationToken cancellationToken = default)
    {
        servico = NormalizarServico(servico);
        var existentes = await db.ProxyMotores.AsNoTracking()
            .Where(x => x.Servico == servico)
            .ToDictionaryAsync(x => x.Motor, cancellationToken);

        var suportados = MotoresProxy.Suportados(servico);
        return [.. suportados.Select((motor, indice) =>
            existentes.TryGetValue(motor, out var c)
                ? ParaDto(c)
                : new ProxyMotorDto(
                    servico, motor, MotoresProxy.Rotulo(motor),
                    Ativo: true, Ordem: indice, ExigeToken: MotoresProxy.ExigeToken(motor),
                    TokenDefinido: false,
                    TimeoutSegundos: TimeoutPadrao, Tentativas: TentativasPadrao, ParametrosJson: null))];
    }

    public async Task AtualizarAsync(
        string servico, string motor, AtualizarProxyMotorRequest request, CancellationToken cancellationToken = default)
    {
        servico = NormalizarServico(servico);
        motor = NormalizarMotor(servico, motor);

        var c = await db.ProxyMotores.FirstOrDefaultAsync(
            x => x.Servico == servico && x.Motor == motor, cancellationToken);
        if (c is null)
        {
            c = new ProxyMotorConfig
            {
                Id = Guid.CreateVersion7(),
                Servico = servico,
                Motor = motor,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = usuarioAtual.UsuarioId,
            };
            db.ProxyMotores.Add(c);
        }

        c.Ativo = request.Ativo;
        c.Ordem = request.Ordem;
        c.TimeoutSegundos = Math.Clamp(request.TimeoutSegundos, 1, 60);
        c.Tentativas = Math.Clamp(request.Tentativas, 1, 5);
        c.ParametrosJson = string.IsNullOrWhiteSpace(request.ParametrosJson) ? null : request.ParametrosJson.Trim();

        // Vazio = mantém o atual; preenchido = cifra e substitui.
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            c.TokenCifrado = protetor.Proteger(request.Token.Trim());
        }

        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(string Motor, MotorExecucao Cfg)>> ObterMotoresAtivosAsync(
        string servico, CancellationToken cancellationToken = default)
    {
        servico = NormalizarServico(servico);
        var existentes = await db.ProxyMotores.AsNoTracking()
            .Where(x => x.Servico == servico)
            .ToListAsync(cancellationToken);
        var porMotor = existentes.ToDictionary(x => x.Motor);

        var resultado = new List<(string Motor, int Ordem, MotorExecucao Cfg)>();
        foreach (var (motor, indice) in MotoresProxy.Suportados(servico).Select((m, i) => (m, i)))
        {
            porMotor.TryGetValue(motor, out var c);

            // Sem linha no banco: motor ativo por padrão (funciona "out of the box" no Hub).
            var ativo = c?.Ativo ?? true;
            if (!ativo) continue;

            var token = string.IsNullOrEmpty(c?.TokenCifrado) ? null : protetor.Revelar(c.TokenCifrado);

            // Motor que exige token e não tem nenhum cadastrado → fora da cadeia.
            if (MotoresProxy.ExigeToken(motor) && string.IsNullOrWhiteSpace(token)) continue;

            resultado.Add((
                motor,
                c?.Ordem ?? indice,
                new MotorExecucao(
                    token,
                    c?.TimeoutSegundos ?? TimeoutPadrao,
                    c?.Tentativas ?? TentativasPadrao,
                    c?.ParametrosJson)));
        }

        return [.. resultado.OrderBy(x => x.Ordem).ThenBy(x => x.Motor).Select(x => (x.Motor, x.Cfg))];
    }

    public async Task<MotorExecucao> ObterExecucaoAsync(
        string servico, string motor, CancellationToken cancellationToken = default)
    {
        servico = NormalizarServico(servico);
        motor = NormalizarMotor(servico, motor);

        var c = await db.ProxyMotores.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Servico == servico && x.Motor == motor, cancellationToken);

        var token = string.IsNullOrEmpty(c?.TokenCifrado) ? null : protetor.Revelar(c.TokenCifrado);

        return new MotorExecucao(
            token,
            c?.TimeoutSegundos ?? TimeoutPadrao,
            c?.Tentativas ?? TentativasPadrao,
            c?.ParametrosJson);
    }

    private static ProxyMotorDto ParaDto(ProxyMotorConfig c) => new(
        c.Servico,
        c.Motor,
        MotoresProxy.Rotulo(c.Motor),
        c.Ativo,
        c.Ordem,
        MotoresProxy.ExigeToken(c.Motor),
        !string.IsNullOrEmpty(c.TokenCifrado),
        c.TimeoutSegundos,
        c.Tentativas,
        c.ParametrosJson);

    private static string NormalizarServico(string servico)
    {
        var s = (servico ?? string.Empty).Trim().ToLowerInvariant();
        if (!ServicosProxy.EhValido(s))
        {
            throw new ValidacaoException("proxy.servico_invalido",
                $"Serviço '{servico}' inválido. Válidos: {string.Join(", ", ServicosProxy.Todos)}.");
        }
        return s;
    }

    private static string NormalizarMotor(string servico, string motor)
    {
        var m = (motor ?? string.Empty).Trim().ToLowerInvariant();
        if (!MotoresProxy.EhSuportado(servico, m))
        {
            throw new ValidacaoException("proxy.motor_invalido",
                $"Motor '{motor}' não é suportado para {ServicosProxy.Rotulo(servico)}.");
        }
        return m;
    }
}
