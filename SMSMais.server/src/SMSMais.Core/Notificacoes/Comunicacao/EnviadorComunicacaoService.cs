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

    /// <param name="lembreteLigado">Chave de operação do lembrete (menu Confirmações).</param>
    private FinalidadeComunicacao[] FinalidadesHabilitadas(bool lembreteLigado)
    {
        var lista = new List<FinalidadeComunicacao>(4);
        if (_options.EnviarConfirmacaoAgendamento) lista.Add(FinalidadeComunicacao.ConfirmacaoAgendamento);
        if (_options.EnviarExameLiberado) lista.Add(FinalidadeComunicacao.ExameLiberado);
        if (_options.EnviarLaudoPronto) lista.Add(FinalidadeComunicacao.LaudoPronto);
        if (_options.EnviarLembreteAgendamento && lembreteLigado)
            lista.Add(FinalidadeComunicacao.LembreteAgendamento);
        return [.. lista];
    }

    /// <summary>Mensagem sobre a AGENDA (confirmação e lembrete) respeita a janela de horário.
    /// Resultado de exame e laudo não: são a resposta a algo que o paciente está esperando.</summary>
    private static bool EhSobreAgendamento(FinalidadeComunicacao f) =>
        f is FinalidadeComunicacao.ConfirmacaoAgendamento or FinalidadeComunicacao.LembreteAgendamento;

    private async Task ExecutarUmaPassagemAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();
        var servico = scope.ServiceProvider.GetRequiredService<IComunicacaoPacienteService>();

        var agora = DateTime.UtcNow;

        // Regras do menu Confirmações: vazão por rodada e janela de horário. Fora da janela a
        // confirmação nem é selecionada (fica empilhada); a exceção é a que acabou de ser liberada
        // pela verificação cadastral — o paciente está na conversa esperando.
        var regras = await scope.ServiceProvider
            .GetRequiredService<Confirmacoes.IConfirmacaoConfiguracaoService>().ObterAsync(ct);

        var habilitadas = FinalidadesHabilitadas(regras.LembreteHabilitado);
        if (habilitadas.Length == 0) return;
        var max = Math.Clamp(regras.MaximoPorPassagem,
            1, Confirmacoes.ConfirmacaoConfiguracaoService.MaximoPorPassagemTeto);
        var janelaAberta = Confirmacoes.JanelaEnvioConfirmacao.Dentro(
            agora, TimeOnly.Parse(regras.HoraInicioEnvio), TimeOnly.Parse(regras.HoraFimEnvio));

        // A régua de "é sobre agendamento?" vira DADO antes da consulta, não chamada de método
        // dentro dela. `EhSobreAgendamento(n.Finalidade)` na árvore de expressão é um método C#
        // que o EF não traduz, e a passagem inteira morria com `InvalidOperationException` — só
        // FORA da janela, porque dentro dela o `true || …` some com o termo antes de chegar ao
        // tradutor. O resultado era o pior tipo de falha: o enviador se matava de minuto em
        // minuto justamente no horário em que só devia sair o que ignora a janela (laudo, TFD),
        // e o sintoma era um log repetido que ninguém lia — nada deixava de "estar ligado".
        var sobreAgendamento = habilitadas.Where(EhSobreAgendamento).ToArray();

        var pendentes = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(n => n.Status == StatusComunicacao.Pendente
                        && n.ProximaTentativaEm != null
                        && n.ProximaTentativaEm <= agora
                        && habilitadas.Contains(n.Finalidade)
                        && (janelaAberta
                            || !sobreAgendamento.Contains(n.Finalidade)
                            || n.IgnorarJanelaHorario))
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
