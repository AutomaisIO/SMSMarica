using System.Threading.Channels;
using SMSMarica.Core.Integracoes.Pep.Estrategias;

namespace SMSMarica.Core.Integracoes.Pep.Background;

/// <summary>Job de importação enfileirado para o runner em background.</summary>
public sealed record PepImportacaoJob(Guid ExecucaoId, Guid FonteId, OpcoesImportacao Opcoes, Guid? UsuarioId);

/// <summary>Fila (singleton) entre o disparo HTTP e o runner em background.</summary>
public interface IPepSincronizacaoFila
{
    /// <summary>Enfileira um job. Lança se já houver um pendente (capacidade 1).</summary>
    bool TentarEnfileirar(PepImportacaoJob job);

    ChannelReader<PepImportacaoJob> Reader { get; }
}

public sealed class PepSincronizacaoFila : IPepSincronizacaoFila
{
    // Capacidade 1 + rejeição: garante no máximo 1 importação na fila (o serviço já barra
    // disparo concorrente, isto é o cinto de segurança).
    private readonly Channel<PepImportacaoJob> _channel =
        Channel.CreateBounded<PepImportacaoJob>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public bool TentarEnfileirar(PepImportacaoJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<PepImportacaoJob> Reader => _channel.Reader;
}
