using Pgvector;

namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Pedaço de um documento de conhecimento + seu embedding (vector(1024)), recuperado
/// por similaridade no momento da pergunta para compor o contexto enviado à IA.
/// </summary>
public class IaChunkConhecimento
{
    public Guid Id { get; set; }
    public Guid DocumentoId { get; set; }
    public IaDocumentoConhecimento? Documento { get; set; }
    public Guid FonteId { get; set; }

    public int Ordem { get; set; }
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>Embedding do chunk (dimensão do provedor de embeddings, ex.: 1024 = voyage-3).</summary>
    public Vector? Embedding { get; set; }

    public DateTime CriadoEm { get; set; }
}
