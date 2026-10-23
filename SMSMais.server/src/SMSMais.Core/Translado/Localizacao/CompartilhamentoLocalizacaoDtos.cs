using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Localizacao;

/// <summary>
/// A viagem de hoje em que o paciente pode compartilhar a localização agora (ADR-0079).
/// <paramref name="Unidade"/>: o destino da ida ou o lugar do resgate. <paramref name="HoraBusca"/>: a
/// busca prevista da ida (nula no resgate). <paramref name="Desde"/> e <paramref name="UltimoEnvioEm"/>
/// só vêm enquanto ele compartilha.
/// </summary>
public sealed record LocalizacaoViagemDto(
    Guid SessaoId,
    TrechoViagem Trecho,
    string Unidade,
    TimeOnly? HoraBusca,
    bool Compartilhando,
    DateTime? Desde,
    DateTime? UltimoEnvioEm);

/// <summary>
/// Uma posição do celular. <paramref name="CapturadaEm"/> é quando o aparelho mediu (UTC);
/// <paramref name="Simulada"/>, se o aparelho disse que ela vem de um app de GPS falso.
/// </summary>
public sealed record AtualizarLocalizacaoRequest(
    double Latitude,
    double Longitude,
    double? PrecisaoM,
    DateTime CapturadaEm,
    bool Simulada = false);

/// <summary>A última posição de um passageiro da rota do tablet que está compartilhando.</summary>
public sealed record LocalizacaoPassageiroDto(
    Guid SessaoId,
    TrechoViagem Trecho,
    double Latitude,
    double Longitude,
    double? PrecisaoM,
    DateTime CapturadaEm,
    bool Simulada);

/// <summary>
/// O que o painel vê de quem compartilha numa rota: desde quando e a idade e a precisão da última
/// posição — sem a coordenada (ADR-0079 §5).
/// </summary>
public sealed record CompartilhamentoDaRotaDto(
    Guid SessaoId,
    TrechoViagem Trecho,
    DateTime Desde,
    DateTime? UltimaPosicaoEm,
    double? PrecisaoM,
    bool Simulada);
