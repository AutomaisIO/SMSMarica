namespace SMSMais.Core.Regulacao.Anexos;

/// <summary>
/// Onde ficam os arquivos das caixinhas de exigência (ADR-0052).
///
/// <para>Interface própria, em vez de usar <c>IArmazenamentoArquivos</c> direto, por duas razões:
/// a chave dos anexos da regulação tem prefixo próprio (<c>Regulacao/…</c>) para não se misturar
/// com os PDFs de exame, e o serviço de exigências fica testável com um store em memória sem
/// precisar de Spaces.</para>
/// </summary>
public interface IArquivoExigenciaStore
{
    /// <summary><c>Regulacao/{pacienteId}/{arquivoId}.{extensao}</c>.</summary>
    string MontarChave(Guid pacienteId, Guid arquivoId, string extensao);

    Task SalvarAsync(string chave, byte[] conteudo, CancellationToken ct);

    /// <summary><c>null</c> se a chave não existir.</summary>
    Task<byte[]?> LerAsync(string chave, CancellationToken ct);

    /// <summary>Idempotente — não falha se já não existir.</summary>
    Task ExcluirAsync(string chave, CancellationToken ct);
}
