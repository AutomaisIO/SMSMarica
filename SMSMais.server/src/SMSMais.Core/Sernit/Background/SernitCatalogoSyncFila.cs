using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Sernit.Background;

/// <summary>
/// Fila da cópia do catálogo do SERNIT. A cópia leva minutos e roda FORA do request (fechar a aba
/// não a mata); o endpoint só enfileira. Capacidade 1, recusando em vez de esperar (a sessão é
/// única e o índice único não tolera duas cópias concorrentes). Espelho do SER-RJ.
/// </summary>
public interface ISernitCatalogoSyncFila
{
    bool TentarEnfileirar(bool refazerTudo);
    ValueTask<bool> LerAsync(CancellationToken cancellationToken);
    void Liberar(string? erro = null);

    bool EmExecucao { get; }
    string? UltimoErro { get; }
    DateTime? UltimoFimEm { get; }
}

public sealed class SernitCatalogoSyncFila : ISernitCatalogoSyncFila
{
    private readonly Channel<bool> _canal =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    private int _ocupado;

    public bool EmExecucao => Volatile.Read(ref _ocupado) == 1;
    public string? UltimoErro { get; private set; }
    public DateTime? UltimoFimEm { get; private set; }

    public bool TentarEnfileirar(bool refazerTudo)
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0) return false;
        if (_canal.Writer.TryWrite(refazerTudo))
        {
            UltimoErro = null;
            return true;
        }

        Volatile.Write(ref _ocupado, 0);
        return false;
    }

    public ValueTask<bool> LerAsync(CancellationToken cancellationToken) =>
        _canal.Reader.ReadAsync(cancellationToken);

    public void Liberar(string? erro = null)
    {
        UltimoErro = erro;
        UltimoFimEm = DateTime.UtcNow;
        Volatile.Write(ref _ocupado, 0);
    }
}

/// <summary>Roda a cópia do catálogo do SERNIT fora de qualquer requisição (retomável: recurso já
/// lido fica marcado). Espelho do SER-RJ.</summary>
public sealed class SernitCatalogoSyncRunner(
    ISernitCatalogoSyncFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<SernitCatalogoSyncRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool refazerTudo;
            try
            {
                refazerTudo = await fila.LerAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<ISernitCatalogoSyncService>();
                var r = await sync.SincronizarAsync(refazerTudo, stoppingToken);

                logger.LogInformation(
                    "SERNIT/catálogo: cópia terminada — {Recursos} recursos, {Campos} campos, "
                    + "{Listas} listas, {Falhas} falhas em {Seg}s.",
                    r.Recursos, r.Campos, r.Listas, r.Falhas, r.DuracaoSegundos);

                fila.Liberar();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                fila.Liberar("Interrompida pelo desligamento do serviço. Rode de novo para continuar.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SERNIT/catálogo: a cópia falhou.");
                fila.Liberar(ex.Message);
            }
        }
    }
}
