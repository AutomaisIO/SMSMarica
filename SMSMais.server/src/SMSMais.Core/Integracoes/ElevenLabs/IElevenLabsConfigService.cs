namespace SMSMais.Core.Integracoes.ElevenLabs;

/// <summary>
/// Configuração (linha única) da ElevenLabs — fala-para-texto (STT) do Agente IA.
/// Segredo cifrado em repouso (write-only). Espelha <c>ITfdConfigService</c> (Google/WhatsApp).
/// </summary>
public interface IElevenLabsConfigService
{
    Task<ElevenLabsConfigDto> ObterAsync(CancellationToken ct = default);
    Task AtualizarAsync(AtualizarElevenLabsConfigRequest request, CancellationToken ct = default);

    /// <summary>Fixa a voz ativa (voice_id) — usado ao escolher uma voz da biblioteca.</summary>
    Task DefinirVozAsync(string vozId, CancellationToken ct = default);

    /// <summary>Resolve o contexto com a chave revelada. Lança se não configurada/inativa/sem chave.</summary>
    Task<ElevenLabsContexto> ObterContextoAsync(CancellationToken ct = default);
}
