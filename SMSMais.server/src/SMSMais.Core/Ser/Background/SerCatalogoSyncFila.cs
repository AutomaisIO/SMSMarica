using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Ser.Background;

/// <summary>
/// Fila da cópia do catálogo do SER.
///
/// <para><b>Por que a cópia saiu de dentro do request</b> (incidente de 10/08/2026): ela leva ~10
/// minutos e rodava síncrona no endpoint. O proxy desistia da espera, o navegador mostrava
/// "Network Error" — e, pior, o ASP.NET cancelava o <c>CancellationToken</c> da requisição
/// abortada, então o laço obedecia e a cópia MORRIA no meio. Parou em 143 de 203 recursos, e o
/// operador não tinha como saber se tinha falhado ou não.</para>
///
/// <para>Agora o endpoint só enfileira e responde na hora; quem trabalha é o runner, com o
/// tempo de vida do serviço e não o da conexão. A tela acompanha pelo progresso no banco.</para>
///
/// <para><b>Capacidade 1, recusando em vez de esperar</b> — duas cópias simultâneas só
/// disputariam a sessão do SER e o índice único.</para>
/// </summary>
public interface ISerCatalogoSyncFila
{
    bool TentarEnfileirar(bool refazerTudo);
    ValueTask<bool> LerAsync(CancellationToken cancellationToken);
    void Liberar(string? erro = null);

    bool EmExecucao { get; }

    /// <summary>Mensagem da última falha, para a tela poder mostrar o motivo em vez de silêncio.</summary>
    string? UltimoErro { get; }

    DateTime? UltimoFimEm { get; }
}

public sealed class SerCatalogoSyncFila : ISerCatalogoSyncFila
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

/// <summary>
/// Roda a cópia do catálogo fora de qualquer requisição.
///
/// <para>Usa o <c>stoppingToken</c> do serviço, não o da conexão: fechar a aba do navegador não
/// pode mais interromper a cópia. Um deploy no meio interrompe — e tudo bem, porque a cópia é
/// retomável: o recurso já lido fica marcado e a próxima rodada pega só o que faltou.</para>
/// </summary>
public sealed class SerCatalogoSyncRunner(
    ISerCatalogoSyncFila fila,
    IServiceScopeFactory scopeFactory,
    ILogger<SerCatalogoSyncRunner> logger) : BackgroundService
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
                var sync = scope.ServiceProvider.GetRequiredService<ISerCatalogoSyncService>();
                var r = await sync.SincronizarAsync(refazerTudo, stoppingToken);

                logger.LogInformation(
                    "SER/catálogo: cópia terminada — {Recursos} recursos, {Campos} campos, "
                    + "{Listas} listas, {Falhas} falhas em {Seg}s.",
                    r.Recursos, r.Campos, r.Listas, r.Falhas, r.DuracaoSegundos);

                fila.Liberar();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento do serviço. A cópia é retomável — não é erro.
                fila.Liberar("Interrompida pelo desligamento do serviço. Rode de novo para continuar.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SER/catálogo: a cópia falhou.");
                fila.Liberar(ex.Message);
            }
        }
    }
}
