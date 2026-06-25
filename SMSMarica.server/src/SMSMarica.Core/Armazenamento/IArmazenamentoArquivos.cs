namespace SMSMarica.Core.Armazenamento;

/// <summary>
/// Armazenamento de arquivos binários grandes (PDFs de exame, etc.) <b>fora</b> do
/// banco. Diferente de <c>IMidiasService</c> (bytea no Postgres, ativos institucionais
/// pequenos), aqui o conteúdo vive em armazenamento de objeto e o domínio guarda só a
/// <c>ChaveArmazenamento</c>. Implementação única: <see cref="ArmazenamentoSpaces"/>
/// (DigitalOcean Spaces / S3). Não há armazenamento local — se o Spaces estiver
/// indisponível, a operação falha com alerta (sem fallback que gere registro órfão).
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
