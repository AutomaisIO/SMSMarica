using System.Threading.Channels;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura.Background;

/// <summary>Um pedido de varredura do SERNIT enfileirado.</summary>
public sealed record PedidoVarreduraSernit(
    ModoVarreduraSernit Modo,
    DisparoSincronizacao Disparo,
    DateOnly Inicio,
    DateOnly Fim,
    IReadOnlyList<SituacaoSernit>? Situacoes,
    Guid? UsuarioId,
    string? UsuarioNome,
    /// <summary>Execução a RETOMAR (do ponteiro) em vez de criar uma nova. Null = rodada nova.</summary>
    Guid? ExecucaoParaRetomar = null);

/// <summary>
/// Fila de varreduras do SERNIT. <b>Capacidade 1 e recusa em vez de espera</b> — a sessão do SERNIT
/// é única por operador (duas varreduras concorrentes se derrubariam).
/// </summary>
public interface IVarreduraSernitFila
{
    /// <summary>Enfileira. Devolve <c>false</c> se já existe rodada aguardando/rodando.</summary>
    bool TentarEnfileirar(PedidoVarreduraSernit pedido);

    ValueTask<PedidoVarreduraSernit> LerAsync(CancellationToken cancellationToken);

    void Liberar();

    bool TemTrabalho { get; }
}

public sealed class VarreduraSernitFila : IVarreduraSernitFila
{
    private readonly Channel<PedidoVarreduraSernit> _canal =
        Channel.CreateBounded<PedidoVarreduraSernit>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    private int _ocupado;

    public bool TemTrabalho => Volatile.Read(ref _ocupado) == 1;

    public bool TentarEnfileirar(PedidoVarreduraSernit pedido)
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0) return false;
        if (_canal.Writer.TryWrite(pedido)) return true;

        Volatile.Write(ref _ocupado, 0);
        return false;
    }

    public ValueTask<PedidoVarreduraSernit> LerAsync(CancellationToken cancellationToken) =>
        _canal.Reader.ReadAsync(cancellationToken);

    public void Liberar() => Volatile.Write(ref _ocupado, 0);
}
