using System.Threading.Channels;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;

/// <param name="UsuarioId">Capturado NA REQUEST — o runner não tem HttpContext. NULL no disparo
/// agendado: não existe usuário-robô, a autoria ali é o <paramref name="Disparo"/>.</param>
public sealed record EscalasSincronizacaoJob(DisparoSincronizacao Disparo, Guid? UsuarioId, string? UsuarioNome);

public interface IEscalasSincronizacaoFila
{
    /// <summary>False = já há uma sincronização esperando (só 1 por vez).</summary>
    bool TentarEnfileirar(EscalasSincronizacaoJob job);

    ChannelReader<EscalasSincronizacaoJob> Reader { get; }
}

/// <summary>
/// Fila de 1 posição. Duas sincronizações em paralelo baixariam o mesmo arquivo de 5,9 MB duas
/// vezes e disputariam o upsert das mesmas 17 mil linhas — gasto dobrado para o mesmo resultado.
/// </summary>
public sealed class EscalasSincronizacaoFila : IEscalasSincronizacaoFila
{
    private readonly Channel<EscalasSincronizacaoJob> _channel =
        Channel.CreateBounded<EscalasSincronizacaoJob>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public bool TentarEnfileirar(EscalasSincronizacaoJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<EscalasSincronizacaoJob> Reader => _channel.Reader;
}
