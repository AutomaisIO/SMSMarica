namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Configuração global do módulo IA (linha única). Token e modelo do provedor de IA
/// e do provedor de embeddings ficam aqui, cifrados em repouso. Gerida pela tela de
/// Configuração — rótulos genéricos na UI (sem expor a marca do provedor).
/// </summary>
public class IaConfiguracao
{
    public Guid Id { get; set; }

    /// <summary>Identificador do provedor de IA (ex.: "anthropic"). Não exibido na UI.</summary>
    public string Provedor { get; set; } = string.Empty;

    /// <summary>Token da API de IA, cifrado (IDataProtector). Write-only na API.</summary>
    public string? TokenCifrado { get; set; }

    /// <summary>Modelo de geração (ex.: "claude-opus-4-8").</summary>
    public string Modelo { get; set; } = string.Empty;

    public string ProvedorEmbeddings { get; set; } = string.Empty;
    public string? TokenEmbeddingsCifrado { get; set; }
    public string ModeloEmbeddings { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
