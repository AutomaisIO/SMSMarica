using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Ser.Pacientes;

namespace SMSMarica.Core.Ser.Background;

/// <summary>
/// Leva ao hub FHIR os pacientes que a varredura carimbou como alterados.
///
/// <para><b>Por que é um worker e não um passo da varredura.</b> A rodada completa do SER leva
/// ~7 horas e passa por 25 mil solicitações. Conciliar dentro dela somaria milhares de
/// requisições ao hub e, sobretudo, faria uma indisponibilidade do hub <b>derrubar a varredura</b>
/// — a leitura do SER passaria a depender da saúde de outro serviço, sem nenhuma razão. Aqui os
/// dois são independentes: a varredura carimba, este worker leva, e se o hub estiver fora a fila
/// espera.</para>
///
/// <para><b>Drena a fila INTEIRA a cada acordada</b>, lote a lote, e só então dorme. A primeira
/// versão parava em 100 por passagem: com a fila do dia a dia — dezenas — dava no mesmo, mas na
/// carga inicial de 19 mil viraria ~32 horas para um trabalho de minutos. Não há o que poupar
/// aqui: o hub responde em localhost e o espelho do SER é a nossa própria base, então não existe
/// serviço de terceiro para preservar nem limite de taxa a respeitar.</para>
///
/// <para>Os lotes continuam existindo por causa da memória e do change tracker — cada um em seu
/// próprio escopo, com DbContext novo —, não como freio.</para>
/// </summary>
public sealed class SerConciliacaoPacienteRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<SerConciliacaoPacienteRunner> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    /// <summary>Tamanho do lote: recorte de memória, não freio. A fila é drenada até o fim.</summary>
    private const int Lote = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Espera o serviço subir por completo antes da primeira passagem: no boot o pool de
        // conexões está em disputa com a migração e com o resto do startup.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var total = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Escopo NOVO por lote: com um só, o change tracker acumularia as 19 mil
                    // entidades da carga inicial e a gravação iria ficando mais lenta a cada lote.
                    using var escopo = scopeFactory.CreateScope();
                    var svc = escopo.ServiceProvider.GetRequiredService<ISerBackfillPacientesService>();
                    var r = await svc.ExecutarPendentesAsync(Lote, stoppingToken);

                    if (r.Pacientes == 0) break;               // fila vazia
                    total += r.Pacientes;

                    // GUARDA DE GIRO EM FALSO: falha não limpa a marca, então um lote 100% falho
                    // voltaria idêntico no próximo `while` — laço infinito martelando o hub.
                    // Parar aqui devolve o caso para a próxima acordada, daqui a 10 minutos.
                    if (r.Falhas == r.Pacientes)
                    {
                        logger.LogWarning(
                            "SER/conciliação: lote inteiro falhou ({Qtd}). Parando a drenagem "
                            + "para não girar em falso; retoma na próxima passagem.", r.Falhas);
                        break;
                    }
                }

                if (total > 0)
                {
                    logger.LogInformation("SER/conciliação: fila drenada — {Total} paciente(s).", total);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Nada aqui pode matar o worker: a fila é durável e a próxima passagem retenta.
                logger.LogError(ex, "SER/conciliação: passagem falhou. Tentando de novo em {Min} min.",
                    Intervalo.TotalMinutes);
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
