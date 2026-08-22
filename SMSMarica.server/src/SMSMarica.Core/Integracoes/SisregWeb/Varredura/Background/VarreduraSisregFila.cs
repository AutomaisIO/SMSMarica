using System.Threading.Channels;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura.Background;

/// <summary>
/// Uma varredura a executar.
/// </summary>
/// <param name="UsuarioId">Capturado NA REQUEST — dentro do runner não há usuário logado.
/// NULL no disparo agendado: não existe usuário-robô, a autoria é o <paramref name="Disparo"/>.</param>
/// <param name="SuprimirConfirmacao">True nas varreduras manuais POR PERÍODO (backfill): importar
/// agendamento passado não pode disparar WhatsApp sobre exame que já aconteceu. Força o gate de
/// confirmação a "não enviar", independentemente da configuração da unidade.</param>
public sealed record VarreduraJob(
    Guid ExecucaoId,
    Guid UnidadeId,
    DisparoSincronizacao Disparo,
    Guid? UsuarioId,
    bool SuprimirConfirmacao = false);

public interface IVarreduraSisregFila
{
    /// <summary>False = já há uma varredura esperando (só 1 por vez).</summary>
    bool TentarEnfileirar(VarreduraJob job);

    ChannelReader<VarreduraJob> Reader { get; }
}

/// <summary>
/// Fila de 1 posição. Não é só sobre concorrência de escrita: todas as unidades saem para o SISREG
/// pelo <b>mesmo IP</b> (túnel WireGuard → MikroTik), então varrer duas em paralelo dobraria o
/// ritmo de requisições sem dobrar o orçamento — só chegaria mais rápido ao CAPTCHA.
///
/// <para>Fila SEPARADA da importação de TXT de propósito: com fila compartilhada, o motor da
/// madrugada e o upload manual do operador se bloqueariam mutuamente. Quem evita a colisão real
/// entre os dois é o scheduler, que não dispara enquanto houver importação viva.</para>
/// </summary>
public sealed class VarreduraSisregFila : IVarreduraSisregFila
{
    private readonly Channel<VarreduraJob> _channel =
        Channel.CreateBounded<VarreduraJob>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public bool TentarEnfileirar(VarreduraJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<VarreduraJob> Reader => _channel.Reader;
}
