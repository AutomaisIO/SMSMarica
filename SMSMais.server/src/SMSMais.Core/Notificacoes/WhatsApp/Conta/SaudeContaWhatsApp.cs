using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Tfd.Configuracao;

namespace SMSMais.Core.Notificacoes.WhatsApp.Conta;

/// <summary>Um problema que a Meta aponta no <c>health_status</c> (número, WABA, empresa ou App).</summary>
public sealed record ProblemaSaudeWhatsApp(string Entidade, string PodeEnviar, string? Codigo, string? Descricao, string? Solucao);

/// <summary>O que a Meta diz do número: AVAILABLE, LIMITED ou BLOCKED, e por quê.</summary>
public sealed record SaudeNumeroWhatsApp(string PodeEnviar, IReadOnlyList<ProblemaSaudeWhatsApp> Problemas)
{
    public bool Bloqueado => string.Equals(PodeEnviar, "BLOCKED", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Pergunta ao Automais.Zap (<c>GET /v1/conta/saude</c>) o <c>health_status</c> do número desta
/// instância. A Meta diz ali "BLOCKED por pagamento" antes de o primeiro envio ser recusado — e diz
/// "AVAILABLE" quando a conta volta, sem precisar queimar uma mensagem de paciente como sonda.
///
/// <para>Relay sem o endpoint (versão antiga) devolve 404: a consulta só fica muda, e o freio segue
/// pela sonda — sem ordem obrigatória de deploy entre os dois.</para>
/// </summary>
public sealed class SaudeContaWhatsAppCliente(
    HttpClient http,
    ITfdConfigService config,
    IConfiguration configuration,
    ILogger<SaudeContaWhatsAppCliente> logger)
{
    /// <summary>Null quando não há o que perguntar (simulado, sem conta, relay antigo ou fora).</summary>
    public async Task<SaudeNumeroWhatsApp?> ConsultarAsync(CancellationToken ct)
    {
        if (configuration.GetValue("Tfd:WhatsApp:Simular", defaultValue: false)) return null;
        TfdWhatsAppContexto ctx;
        try { ctx = await config.ObterWhatsAppContextoAsync(ct); }
        catch (ValidacaoException) { return null; }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ctx.ZapBaseUrl.TrimEnd('/')}/v1/conta/saude");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.ZapToken);
            using var resp = await http.SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null; // relay ainda sem o endpoint
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Consulta de saúde do WhatsApp recusada pelo Zap ({Status}): {Corpo}",
                    (int)resp.StatusCode, corpo.Length <= 300 ? corpo : corpo[..300]);
                return null;
            }
            return Parsear(corpo, ctx.PhoneNumberId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Consulta de saúde do WhatsApp falhou (rede).");
            return null;
        }
    }

    /// <summary>
    /// Lê <c>{"numeros":[{"phone_number_id","pode_enviar","problemas":[{entidade,pode_enviar,codigo,descricao,solucao}]}]}</c>
    /// e devolve o do número desta instância (ou null se ele não veio, ou veio com erro de consulta).
    /// </summary>
    internal static SaudeNumeroWhatsApp? Parsear(string corpo, string phoneNumberId)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (!doc.RootElement.TryGetProperty("numeros", out var numeros) || numeros.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var n in numeros.EnumerateArray())
            {
                if (Texto(n, "phone_number_id") != phoneNumberId) continue;
                var pode = Texto(n, "pode_enviar");
                if (string.IsNullOrEmpty(pode)) return null; // a Meta não respondeu sobre este número

                var problemas = new List<ProblemaSaudeWhatsApp>();
                if (n.TryGetProperty("problemas", out var ps) && ps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in ps.EnumerateArray())
                        problemas.Add(new ProblemaSaudeWhatsApp(
                            Texto(p, "entidade") ?? "?", Texto(p, "pode_enviar") ?? "?",
                            Texto(p, "codigo"), Texto(p, "descricao"), Texto(p, "solucao")));
                }
                return new SaudeNumeroWhatsApp(pode, problemas);
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A explicação para a faixa a partir do que a Meta apontou como bloqueado.</summary>
    internal static ErroDeContaWhatsApp ErroDe(SaudeNumeroWhatsApp saude)
    {
        var bloqueios = saude.Problemas
            .Where(p => string.Equals(p.PodeEnviar, "BLOCKED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var principal = bloqueios.FirstOrDefault(p => p.Descricao is not null) ?? bloqueios.FirstOrDefault();
        var detalhe = principal is null
            ? "sem detalhe"
            : string.Join(" ", new[] { principal.Descricao, principal.Solucao }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new ErroDeContaWhatsApp(
            principal?.Codigo ?? "saude",
            $"A Meta informa que a conta NÃO PODE ENVIAR ({principal?.Entidade ?? "conta"}): {detalhe}");
    }

    private static string? Texto(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                _ => null,
            }
            : null;
}

/// <summary>
/// Consulta a saúde da conta de tempos em tempos (padrão 5 min) e mantém o
/// <see cref="ISituacaoContaWhatsApp"/> em dia: BLOCKED para, AVAILABLE/LIMITED libera. LIMITED não
/// para — a conta envia, só com teto.
/// </summary>
public sealed class SaudeContaWhatsAppWorker(
    IServiceScopeFactory scopeFactory,
    ISituacaoContaWhatsApp situacao,
    IOptions<ContaWhatsAppOptions> options,
    ILogger<SaudeContaWhatsAppWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsultarUmaVezAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha na consulta de saúde da conta do WhatsApp — tenta de novo no próximo ciclo.");
            }

            var intervalo = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervaloConsultaSaudeMinutos));
            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ConsultarUmaVezAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var cliente = scope.ServiceProvider.GetRequiredService<SaudeContaWhatsAppCliente>();
        var saude = await cliente.ConsultarAsync(ct);
        if (saude is null) return;

        situacao.RegistrarConsultaSaude();
        if (saude.Bloqueado)
            situacao.Parar(SaudeContaWhatsAppCliente.ErroDe(saude), OrigemSituacaoConta.SaudeMeta,
                JsonSerializer.Serialize(saude.Problemas));
        else
            situacao.Liberar(OrigemSituacaoConta.SaudeMeta);
    }
}
