using System.Threading.Channels;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;

/// <summary>
/// Uma resolução em lote de pendências, já com os IDs escolhidos.
///
/// <para><b>Os IDs são um snapshot da hora do clique</b>, não uma consulta feita no runner: uma
/// varredura pode terminar no meio do lote e despejar pendências novas, e o operador que autorizou
/// "resolver estas 260" não autorizou consumir a fonte de cadastro com o que chegou depois.</para>
/// </summary>
/// <param name="UsuarioId">Capturado NA REQUEST — dentro do runner não há usuário logado.</param>
/// <param name="UnidadeAtivaId">Idem: é o contexto que resolve a unidade executante.</param>
public sealed record ResolucaoPendenciasJob(
    Guid ExecucaoId,
    IReadOnlyList<Guid> FalhaIds,
    Guid? UsuarioId,
    Guid? UnidadeAtivaId);

public interface IResolucaoPendenciasFila
{
    /// <summary>False = já há um lote na fila (só 1 por vez).</summary>
    bool TentarEnfileirar(ResolucaoPendenciasJob job);

    ChannelReader<ResolucaoPendenciasJob> Reader { get; }
}

/// <summary>
/// Fila de 1 posição, como a de arquivos: dois lotes concorrentes disputariam a mesma sessão do
/// SISREG/SER (serializada por semáforo) e ainda competiriam na idempotência por nº da solicitação.
/// </summary>
public sealed class ResolucaoPendenciasFila : IResolucaoPendenciasFila
{
    private readonly Channel<ResolucaoPendenciasJob> _channel =
        Channel.CreateBounded<ResolucaoPendenciasJob>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public bool TentarEnfileirar(ResolucaoPendenciasJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<ResolucaoPendenciasJob> Reader => _channel.Reader;
}
