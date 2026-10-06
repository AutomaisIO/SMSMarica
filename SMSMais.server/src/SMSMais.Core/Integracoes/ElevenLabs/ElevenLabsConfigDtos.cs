namespace SMSMais.Core.Integracoes.ElevenLabs;

/// <summary>Estado da integração ElevenLabs. A chave nunca é reexibida (write-only).</summary>
public sealed record ElevenLabsConfigDto(string BaseUrl, string Modelo, bool ChaveConfigurada, bool Ativo);

/// <summary>Chave em branco mantém a que está gravada — a tela nunca reexibe o valor.</summary>
public sealed record AtualizarElevenLabsConfigRequest(string BaseUrl, string? Modelo, string? ApiKey, bool Ativo);

/// <summary>Contexto resolvido (segredo revelado) da ElevenLabs, para quem for chamar a API de STT.</summary>
public sealed record ElevenLabsContexto(string BaseUrl, string Modelo, string ApiKey);
