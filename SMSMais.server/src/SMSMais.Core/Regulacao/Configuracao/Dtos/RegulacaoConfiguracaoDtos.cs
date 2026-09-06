using System.Text.Json;

using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Configuracao.Dtos;

/// <summary>Configuração completa — só para quem tem o módulo de configuração (51).</summary>
public sealed record RegulacaoConfiguracaoDto(
    bool PermitirExternoComInterno,
    bool PontaPodeEscolherUnidade,
    bool PontaPodeVerTodasUnidades,
    bool ExigirCpf,
    string RotuloFila,
    int SisregPrazoEdicaoDias,
    decimal BuscaCorteDistancia,
    decimal BuscaScoreSugestaoPareamento,
    JsonElement RegrasFollowup,
    int AnexoLimiteMb,
    string[] AnexoTiposPermitidos,
    NaoSeiViraRegulacao NaoSeiPadrao,
    DateTime? AtualizadoEm,
    string? AtualizadoPorNome,
    uint RowVersion);

/// <summary>
/// Subconjunto que o wizard precisa, liberado a qualquer solicitante (47).
///
/// <para>É um DTO separado de propósito: os cortes da busca, o prazo do SISREG e as regras de
/// follow-up são parâmetros de operação e não têm por que trafegar para a ponta.</para>
/// </summary>
public sealed record RegulacaoConfiguracaoFluxoDto(
    bool PermitirExternoComInterno,
    bool PontaPodeVerTodasUnidades,
    bool ExigirCpf,
    string RotuloFila,
    int AnexoLimiteMb,
    string[] AnexoTiposPermitidos);

public sealed record AtualizarRegulacaoConfiguracaoRequest(
    bool PermitirExternoComInterno,
    bool PontaPodeEscolherUnidade,
    bool PontaPodeVerTodasUnidades,
    bool ExigirCpf,
    string RotuloFila,
    int SisregPrazoEdicaoDias,
    decimal BuscaCorteDistancia,
    decimal BuscaScoreSugestaoPareamento,
    JsonElement? RegrasFollowup,
    int AnexoLimiteMb,
    string[] AnexoTiposPermitidos,
    NaoSeiViraRegulacao NaoSeiPadrao,
    uint RowVersion);
