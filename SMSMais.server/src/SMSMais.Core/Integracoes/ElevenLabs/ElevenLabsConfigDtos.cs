namespace SMSMais.Core.Integracoes.ElevenLabs;

/// <summary>Estado da integração ElevenLabs. A chave nunca é reexibida (write-only).</summary>
public sealed record ElevenLabsConfigDto(
    string BaseUrl, string Modelo, string? VozId, string ModeloTts, double VelocidadeTts,
    bool ChaveConfigurada, bool Ativo);

/// <summary>Chave em branco mantém a que está gravada — a tela nunca reexibe o valor.</summary>
public sealed record AtualizarElevenLabsConfigRequest(
    string BaseUrl, string? Modelo, string? VozId, string? ModeloTts, double? VelocidadeTts,
    string? ApiKey, bool Ativo);

/// <summary>Contexto resolvido (segredo revelado) da ElevenLabs. <see cref="Modelo"/> = STT; <see cref="ModeloTts"/>/<see cref="VozId"/>/<see cref="VelocidadeTts"/> = TTS.</summary>
public sealed record ElevenLabsContexto(
    string BaseUrl, string Modelo, string ApiKey, string? VozId, string ModeloTts, double VelocidadeTts);

/// <summary>Uma voz da conta ElevenLabs, para o seletor na tela.</summary>
public sealed record VozElevenLabs(string VozId, string Nome, string? Idioma, string? Categoria);
