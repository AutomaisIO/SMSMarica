using System.Threading.Channels;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;

/// <summary>Um arquivo do lote a processar. O conteúdo já vem lido (o upload não sobrevive à request).</summary>
/// <param name="UsuarioId">Capturado NA REQUEST — dentro do runner não há usuário logado.</param>
/// <param name="UnidadeAtivaId">Idem: o tenant do operador, que decide a unidade executante.</param>
public sealed record SisregArquivoJob(
    Guid ExecucaoId,
    string NomeArquivo,
    string Conteudo,
    Guid? UsuarioId,
    Guid? UnidadeAtivaId);

/// <summary>Lote inteiro (um upload / um zip) — processado arquivo a arquivo, em série.</summary>
public sealed record SisregImportacaoJob(Guid LoteId, IReadOnlyList<SisregArquivoJob> Arquivos);

public interface ISisregImportacaoFila
{
    /// <summary>False = já há um lote na fila (só 1 por vez).</summary>
    bool TentarEnfileirar(SisregImportacaoJob job);

    ChannelReader<SisregImportacaoJob> Reader { get; }
}

/// <summary>
/// Fila de 1 posição: importar SISREG concorrentemente não é seguro (a idempotência por nº e a
/// criação de unidades/pacientes competiriam entre si), então no máximo um lote espera.
/// </summary>
public sealed class SisregImportacaoFila : ISisregImportacaoFila
{
    private readonly Channel<SisregImportacaoJob> _channel =
        Channel.CreateBounded<SisregImportacaoJob>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public bool TentarEnfileirar(SisregImportacaoJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<SisregImportacaoJob> Reader => _channel.Reader;
}
