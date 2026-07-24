namespace SMSMarica.Core.Inteligencia.Conhecimento.Gestao;

/// <summary>Item da lista de documentos de conhecimento de uma base.</summary>
public sealed record DocumentoResumoDto(
    Guid Id, string Caminho, int Versao, int Tamanho, int Chunks, bool DoRepo, DateTime AtualizadoEm);

public sealed record DocumentoDetalheDto(
    Guid Id, string Caminho, string Conteudo, int Versao, bool DoRepo, DateTime AtualizadoEm);

public sealed record SalvarDocumentoDto(string Caminho, string Conteudo);

public sealed record ExtrairModeloDto(int MaxTabelas = 400);

public sealed record ExtracaoModeloResultado(
    int TotalTabelas, int Documentadas, int TotalFks, int DocumentosGerados, string? Aviso);

/// <summary>
/// Gestão do conhecimento (documentação .md) de uma base do módulo IA: listar/ler/gravar/remover
/// documentos guardados no banco (editáveis pela tela) e extrair o modelo da base (tabelas +
/// relacionamentos) direto do schema, gerando um doc por tabela. Tudo re-chunkado e embeddado.
/// Ver ADR-0023.
/// </summary>
public interface IConhecimentoGestaoService
{
    Task<IReadOnlyList<DocumentoResumoDto>> ListarDocumentosAsync(
        Guid fonteId, CancellationToken ct = default);

    Task<DocumentoDetalheDto> ObterDocumentoAsync(Guid fonteId, Guid docId, CancellationToken ct = default);

    /// <summary>Cria ou atualiza um doc (por <c>docId</c>). Re-chunka e re-embeda.</summary>
    Task<DocumentoDetalheDto> SalvarDocumentoAsync(
        Guid fonteId, Guid? docId, SalvarDocumentoDto dto, CancellationToken ct = default);

    Task RemoverDocumentoAsync(Guid fonteId, Guid docId, CancellationToken ct = default);

    /// <summary>
    /// Lê o schema da base (via a fonte configurada — direto ou por agente proxy) e gera/atualiza
    /// os documentos do modelo: um por tabela + um catálogo. Idempotente pelo caminho.
    /// </summary>
    Task<ExtracaoModeloResultado> ExtrairModeloAsync(
        Guid fonteId, ExtrairModeloDto dto, CancellationToken ct = default);
}
