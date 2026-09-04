using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

/// <summary>
/// Roda a análise dos itens de treinamento fora do caminho da requisição. O clique em "treinar" só
/// marca o item como <see cref="StatusTreinamentoRobo.Analisando"/>; o ciclo (proposta →
/// adversários → juiz → simulação) leva minutos no Fable e não caberia num POST.
///
/// Um item por vez, de propósito: o ciclo lê e escreve o material do robô, e dois itens mexendo no
/// mesmo assunto ao mesmo tempo produziriam regras que nenhum dos dois adversários chegou a ver.
/// </summary>
public sealed class RoboTreinamentoWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RoboTreinamentoWorker> logger) : BackgroundService
{
    /// <summary>Depois disso o item para em <see cref="StatusTreinamentoRobo.Falhou"/> em vez de
    /// ficar em laço queimando token.</summary>
    private const int MaxTentativas = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(
            Math.Max(10, configuration.GetValue("RoboTreinamento:IntervaloSegundos", 20)));

        logger.LogInformation("RoboTreinamentoWorker iniciado (intervalo {Intervalo}s).", intervalo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessarProximoAsync(stoppingToken)) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na passagem do RoboTreinamentoWorker.");
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Devolve <c>true</c> se processou algo (para drenar a fila sem esperar o intervalo).</summary>
    private async Task<bool> ProcessarProximoAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmsMaisDbContext>();

        var item = await db.RoboTreinamentoItens
            .Where(i => i.Status == StatusTreinamentoRobo.Analisando)
            .OrderBy(i => i.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (item is null) return false;

        if (item.TentativasAnalise >= MaxTentativas)
        {
            item.Status = StatusTreinamentoRobo.Falhou;
            item.ErroMensagem ??= $"A análise falhou {MaxTentativas} vezes seguidas.";
            item.AtualizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        // Conta a tentativa ANTES de rodar: se o processo morrer no meio (deploy, OOM), o item não
        // volta para a fila indefinidamente.
        item.TentativasAnalise++;
        item.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var agente = scope.ServiceProvider.GetRequiredService<IRoboTreinadorAgente>();
        var simulador = scope.ServiceProvider.GetRequiredService<IRoboTreinamentoSimulador>();

        try
        {
            var resultado = await agente.AnalisarAsync(item, ct);

            item.Analise = resultado.Parecer;
            item.AnaliseJson = resultado.RastroJson;
            item.Modelo = RoboTreinadorAgente.Modelo;
            item.TokensEntrada += resultado.TokensEntrada;
            item.TokensSaida += resultado.TokensSaida;
            item.CustoUsd += resultado.CustoUsd ?? 0m;
            item.AnalisadoEm = DateTime.UtcNow;
            item.AtualizadoEm = DateTime.UtcNow;

            item.Status = resultado.AbriuPendencia
                ? StatusTreinamentoRobo.AguardandoHumano
                : resultado.AlteracoesAplicadas > 0
                    ? StatusTreinamentoRobo.SimulacaoPendente
                    : resultado.Descartado
                        ? StatusTreinamentoRobo.Descartado
                        : StatusTreinamentoRobo.Concluido;

            // As alterações do agente e o desfecho do item vão juntos: um item "concluído" cujas
            // regras não entraram (ou o contrário) é pior que um item que falhou.
            await db.SaveChangesAsync(ct);

            // A simulação de verificação é automática — foi o que o operador pediu: depois de
            // mexer no robô, a pergunta que importa é "e agora, responde certo?".
            if (item.Status == StatusTreinamentoRobo.SimulacaoPendente && resultado.CasoTeste is { } caso)
                await VerificarAsync(db, simulador, item, caso, ct);

            logger.LogInformation(
                "Treinamento do item {Item}: {Status} ({Alteracoes} alterações, {Custo:0.0000} USD).",
                item.Id, item.Status, resultado.AlteracoesAplicadas, item.CustoUsd);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Análise do item de treinamento {Item} falhou.", item.Id);

            // O contexto pode ter mudanças parciais do ciclo que estourou — descarta e recarrega
            // para gravar só o erro.
            db.ChangeTracker.Clear();
            var atual = await db.RoboTreinamentoItens.FirstOrDefaultAsync(i => i.Id == item.Id, ct);
            if (atual is not null)
            {
                atual.ErroMensagem = ex.Message;
                if (atual.TentativasAnalise >= MaxTentativas) atual.Status = StatusTreinamentoRobo.Falhou;
                atual.AtualizadoEm = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        return true;
    }

    private async Task VerificarAsync(
        SmsMaisDbContext db, IRoboTreinamentoSimulador simulador,
        RoboTreinamentoItem item, CasoTesteTreinamento caso, CancellationToken ct)
    {
        try
        {
            var sim = await simulador.SimularAsync(
                item, caso.Mensagem, caso.Historico, automatica: true, quem: null, ct);

            // Só fecha o item se o ensaio de fato rodou. Um ensaio que estourou deixa o item em
            // "simulação pendente" — o humano ainda precisa ver o robô respondendo.
            if (sim.ErroMensagem is null)
            {
                item.Status = StatusTreinamentoRobo.Concluido;
                item.AtualizadoEm = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Simulação automática do item {Item} falhou.", item.Id);
        }
    }
}
