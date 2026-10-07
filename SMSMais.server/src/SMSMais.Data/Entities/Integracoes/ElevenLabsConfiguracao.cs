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

    /// <summary>Modelo de transcrição (STT). Padrão: scribe_v2 (scribe_v1 foi depreciado).</summary>
    public string Modelo { get; set; } = "scribe_v2";

    /// <summary>Voz da síntese (TTS) — voice_id do ElevenLabs. Em branco = usa a primeira da conta.</summary>
    public string? VozId { get; set; }

    /// <summary>Modelo da síntese (TTS). Padrão: eleven_multilingual_v2.</summary>
    public string ModeloTts { get; set; } = "eleven_multilingual_v2";

    /// <summary>Velocidade da fala (TTS). Faixa do ElevenLabs: 0,7 a 1,2. Padrão: 1,15.</summary>
    public double VelocidadeTts { get; set; } = 1.15;

    /// <summary>Chave da API cifrada. Write-only na API.</summary>
    public string? ApiKeyCifrada { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
