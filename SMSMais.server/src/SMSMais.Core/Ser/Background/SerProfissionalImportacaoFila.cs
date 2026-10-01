using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SMSMais.Core.Ser.Profissionais;

namespace SMSMais.Core.Ser.Background;

/// <summary>
/// Fila da importação dos profissionais do SER (ADR-0065). Mesmo desenho da
/// <see cref="SerCatalogoSyncFila"/>: a leitura são ~47 idas ao SER e não cabe na espera de um
/// request — o endpoint enfileira e responde na hora; a tela acompanha pelo resumo.
/// Capacidade 1, recusando em vez de esperar.
/// </summary>
public interface ISerProfissionalImportacaoFila
{
    bool TentarEnfileirar();
    ValueTask LerAsync(CancellationToken cancellationToken);
    void Liberar(string? erro = null);

    bool EmExecucao { get; }
    string? UltimoErro { get; }
}

public sealed class SerProfissionalImportacaoFila : ISerProfissionalImportacaoFila
{
    private readonly Channel<bool> _canal =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private int _ocupado;

    public bool EmExecucao => Volatile.Read(ref _ocupado) == 1;
    public string? UltimoErro { get; private set; }

    public bool TentarEnfileirar()
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0) return false;
        if (_canal.Writer.TryWrite(true))
        {
            UltimoErro = null;
            return true;
        }

        Volatile.Write(ref _ocupado, 0);
        return false;
    }

    public async ValueTask LerAsync(CancellationToken cancellationToken) =>
        await _canal.Reader.ReadAsync(cancellationToken);

    public void Liberar(string? erro = null)
    {
        UltimoErro = erro;
        Volatile.Write(ref _ocupado, 0);
    }
}

/// <summary>Roda a importação com o tempo de vida do serviço, não o da conexão.</summary>
public sealed class SerProfissionalImportacaoRunner(
    ISerProfissionalImportacaoFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<SerProfissionalImportacaoRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await fila.LerAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var servico = scope.ServiceProvider.GetRequiredService<ISerProfissionalService>();
                var r = await servico.ImportarAsync(stoppingToken);

                logger.LogInformation(
                    "SER/profissionais: importação terminada — {Lidas} linhas, {Novos} novos, "
                    + "{Atualizados} atualizados, {Sumiram} saíram do SER, em {Seg:F0}s.",
                    r.Lidas, r.Novos, r.Atualizados, r.Sumiram, r.DuracaoSegundos);
                fila.Liberar();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                fila.Liberar("Interrompida pelo desligamento do serviço. Rode de novo.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SER/profissionais: a importação falhou.");
                fila.Liberar(ex.Message);
            }
        }
    }
}
