namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Espelho no banco de um arquivo .md de conhecimento versionado no repositório.
/// O repo é a fonte da verdade; esta tabela é sincronizada no deploy/seed e serve
/// de base para o chunking + embeddings (<see cref="IaChunkConhecimento"/>).
/// </summary>
public class IaDocumentoConhecimento
{
    public Guid Id { get; set; }
    public Guid FonteId { get; set; }
    public IaFonte? Fonte { get; set; }

    /// <summary>Caminho relativo do .md no repositório (chave de sincronização).</summary>
    public string Caminho { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>Hash do conteúdo, para detectar mudança e re-embeddar só o necessário.</summary>
    public string Hash { get; set; } = string.Empty;
    public int Versao { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public ICollection<IaChunkConhecimento> Chunks { get; set; } = [];
}
