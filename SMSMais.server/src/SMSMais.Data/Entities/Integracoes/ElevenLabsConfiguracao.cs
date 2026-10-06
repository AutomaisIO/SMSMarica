namespace SMSMais.Data.Entities.Integracoes;

/// <summary>
/// Configuração (linha única) da ElevenLabs — usada para fala-para-texto (STT, modelo Scribe)
/// do áudio que o operador dita ao Agente IA pelo WhatsApp. Chave de API cifrada em repouso
/// (IProtetorSegredos), write-only na API. Mesma filosofia de <c>GeoConfiguracao</c>.
/// </summary>
public class ElevenLabsConfiguracao
{
    public Guid Id { get; set; }

    public string BaseUrl { get; set; } = "https://api.elevenlabs.io/";

    /// <summary>Modelo de transcrição (STT). Padrão: scribe_v1.</summary>
    public string Modelo { get; set; } = "scribe_v1";

    /// <summary>Chave da API cifrada. Write-only na API.</summary>
    public string? ApiKeyCifrada { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
