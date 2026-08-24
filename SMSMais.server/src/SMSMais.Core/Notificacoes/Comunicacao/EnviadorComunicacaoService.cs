using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Comunicacao;

/// <summary>
/// Worker da fila de comunicações ao paciente (mesmo padrão do EnviadorWorklistService):
/// a cada N segundos pega comunicações Pendentes com <c>ProximaTentativaEm</c> vencida —
/// somente das finalidades HABILITADAS (template aprovado na Meta) — e delega a
/// <see cref="IComunicacaoPacienteService.ProcessarTentativaEnvioAsync"/>. Finalidade
/// desabilitada acumula na fila e flui sozinha quando a chave liga.
/// </summary>
public sealed class EnviadorComunicacaoService(
    IServiceScopeFactory scopeFactory,
    IOptions<ComunicacaoPacienteOptions> options,
    ILogger<EnviadorComunicacaoService> logger)
    : BackgroundService
{
    private readonly ComunicacaoPacienteOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Habilitado)
        {
            logger.LogInformation("EnviadorComunicacaoService desabilitado por configuração.");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _options.IntervaloSegundos));
        logger.LogInformation("EnviadorComunicacaoService iniciado — intervalo {Intervalo}.", intervalo);

        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarUmaPassagemAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // shutdown real do host — encerra o worker
            }
            catch (Exception ex)
            {
                // Inclui TaskCanceledException de TIMEOUT do HttpClient (Meta/WhatsApp lento):
                // trata como falha transitória e continua o loop. NUNCA deixa a exceção subir —
                // senão o BackgroundServiceExceptionBehavior=StopHost derruba a API inteira.
                logger.LogError(ex, "Falha na passagem do EnviadorComunicacaoService — vai tentar de novo.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private FinalidadeComunicacao[] FinalidadesHabilitadas()
    {
        var lista = new List<FinalidadeComunicacao>(3);
        if (_options.EnviarConfirmacaoAgendamento) lista.Add(FinalidadeComunicacao.ConfirmacaoAgendamento);
        if (_options.EnviarExameLiberado) lista.Add(FinalidadeComunicacao.ExameLiberado);
        if (_options.EnviarLaudoPronto) lista.Add(FinalidadeComunicacao.LaudoPronto);
        return [.. lista];
    }

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        var habilitadas = FinalidadesHabilitadas();
        if (habilitadas.Length == 0) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        var servico = scope.ServiceProvider.GetRequiredService<IComunicacaoPacienteService>();

        var agora = DateTime.UtcNow;
        var max = Math.Clamp(_options.MaximoPorPassagem, 1, 100);

        var pendentes = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Status == StatusComunicacao.Pendente
                        && n.ProximaTentativaEm != null
                        && n.ProximaTentativaEm <= agora
                        && habilitadas.Contains(n.Finalidade))
            .OrderBy(n => n.ProximaTentativaEm)
            .Take(max)
            .Select(n => n.Id)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;

        logger.LogDebug("Enviador processando {N} comunicações ao paciente.", pendentes.Count);

        foreach (var id in pendentes)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                await servico.ProcessarTentativaEnvioAsync(id, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao processar comunicação {Id}.", id);
            }
        }
    }
}
