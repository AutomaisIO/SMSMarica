namespace SMSMarica.Core.Armazenamento;

/// <summary>
/// Armazenamento de arquivos binários grandes (PDFs de exame, etc.) <b>fora</b> do
/// banco. Diferente de <c>IMidiasService</c> (bytea no Postgres, ativos institucionais
/// pequenos), aqui o conteúdo vive em disco/objeto e o domínio guarda só a
/// <c>ChaveArmazenamento</c>. A implementação local (<see cref="ArmazenamentoLocalDisco"/>)
/// é o padrão; há um costura/seam para uma futura implementação S3
/// (DigitalOcean Spaces) sem mudar os chamadores.
/// </summary>
public interface IArmazenamentoArquivos
{
    /// <summary>
    /// Monta a chave canônica de um documento de exame no formato
    /// <c>{prefixo}/{pacienteId}/{documentoId}.{extensao}</c> (prefixo de configuração,
    /// default <c>arquivos</c>): pasta = UUID do paciente, arquivo = UUID do documento.
    /// Não toca o armazenamento.
    /// </summary>
    string MontarChaveDocumento(Guid pacienteId, Guid documentoId, string extensao = "pdf");

    /// <summary>Persiste o conteúdo sob a chave, criando diretórios/contêineres conforme necessário.</summary>
    Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default);

    /// <summary>Lê o conteúdo da chave; <c>null</c> se não existir.</summary>
    Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>Remove o arquivo da chave (idempotente — não falha se já não existir).</summary>
    Task ExcluirAsync(string chave, CancellationToken cancellationToken = default);
}
