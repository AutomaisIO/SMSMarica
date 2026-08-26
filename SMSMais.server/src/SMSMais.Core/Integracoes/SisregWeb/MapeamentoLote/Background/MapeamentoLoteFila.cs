using System.Threading.Channels;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;

/// <summary>
/// Um lote a executar. As unidades são resolvidas no início da execução (as com CNES + mapeamento
/// já existente), não no enfileiramento.
/// </summary>
/// <param name="UsuarioId">Capturado NA REQUEST — no runner não há usuário logado. NULL no disparo
/// agendado: não existe usuário-robô, a autoria é o <paramref name="Disparo"/>.</param>
public sealed record MapeamentoLoteJob(DisparoSincronizacao Disparo, Guid? UsuarioId);

public interface IMapeamentoLoteFila
{
    /// <summary>False = já há um lote esperando (só 1 por vez).</summary>
    bool TentarEnfileirar(MapeamentoLoteJob job);

    ChannelReader<MapeamentoLoteJob> Reader { get; }
}

/// <summary>
/// Fila de 1 posição. Todas as unidades saem para o SISREG pelo <b>mesmo IP</b>, então rodar dois
/// lotes em paralelo dobraria o ritmo de requisições sem dobrar o orçamento — só chegaria mais
/// rápido ao CAPTCHA.
/// </summary>
public sealed class MapeamentoLoteFila : IMapeamentoLoteFila
{
    private readonly Channel<MapeamentoLoteJob> _channel =
        Channel.CreateBounded<MapeamentoLoteJob>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public bool TentarEnfileirar(MapeamentoLoteJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<MapeamentoLoteJob> Reader => _channel.Reader;
}
