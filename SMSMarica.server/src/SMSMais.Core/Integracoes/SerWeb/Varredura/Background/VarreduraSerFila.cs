using System.Threading.Channels;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Integracoes.SerWeb.Varredura.Background;

/// <summary>Um pedido de varredura enfileirado.</summary>
public sealed record PedidoVarreduraSer(
    ModoVarreduraSer Modo,
    DisparoSincronizacao Disparo,
    DateOnly Inicio,
    DateOnly Fim,
    IReadOnlyList<SituacaoSer>? Situacoes,
    Guid? UsuarioId,
    string? UsuarioNome,
    /// <summary>Execução a RETOMAR (do ponteiro) em vez de criar uma nova. Null = rodada nova.</summary>
    Guid? ExecucaoParaRetomar = null);

/// <summary>
/// Fila de varreduras do SER.
///
/// <para><b>Capacidade 1 e recusa em vez de espera.</b> A sessão do SER é única por operador:
/// duas varreduras concorrentes se derrubariam e ainda derrubariam a sessão do humano que
/// estivesse usando o sistema. Enfileirar várias não ajudaria — a segunda só descobriria o
/// problema tarde. Melhor recusar na hora e dizer ao operador que já tem uma rodando.</para>
/// </summary>
public interface IVarreduraSerFila
{
    /// <summary>Enfileira. Devolve <c>false</c> se já existe rodada aguardando/rodando.</summary>
    bool TentarEnfileirar(PedidoVarreduraSer pedido);

    ValueTask<PedidoVarreduraSer> LerAsync(CancellationToken cancellationToken);

    /// <summary>Sinaliza que o runner terminou (libera a vaga).</summary>
    void Liberar();

    bool TemTrabalho { get; }
}

public sealed class VarreduraSerFila : IVarreduraSerFila
{
    private readonly Channel<PedidoVarreduraSer> _canal =
        Channel.CreateBounded<PedidoVarreduraSer>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    private int _ocupado;

    public bool TemTrabalho => Volatile.Read(ref _ocupado) == 1;

    public bool TentarEnfileirar(PedidoVarreduraSer pedido)
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0) return false;
        if (_canal.Writer.TryWrite(pedido)) return true;

        Volatile.Write(ref _ocupado, 0);
        return false;
    }

    public ValueTask<PedidoVarreduraSer> LerAsync(CancellationToken cancellationToken) =>
        _canal.Reader.ReadAsync(cancellationToken);

    public void Liberar() => Volatile.Write(ref _ocupado, 0);
}
