namespace SMSMais.Core.Estatisticas.Dtos;

/// <summary>
/// Retrato agregado das comunicações WhatsApp num período. Visão gerencial, só leitura;
/// todos os números são contagens (sem PII de paciente/mensagem).
/// </summary>
public sealed record EstatisticasWhatsAppDto(
    DateOnly De,
    DateOnly Ate,
    EstatisticasResumoDto Resumo,
    IReadOnlyList<SerieDiaDto> PorDia,
    IReadOnlyList<RotuloContagemDto> PorCategoria,
    IReadOnlyList<RotuloContagemDto> PorTemplate,
    IReadOnlyList<RotuloContagemDto> PorStatus,
    IReadOnlyList<RotuloContagemDto> PorAtendente,
    RoboConsumoDto Robo,
    /// <summary>Estimativa de custo Meta. <c>null</c> quando o usuário não tem o módulo
    /// <c>EstatisticaCustos</c> (o front esconde o bloco).</summary>
    CustosMetaDto? CustosMeta = null,
    /// <summary>O usuário pode ver custos (tokens/USD do robô e custo Meta)?</summary>
    bool VeCustos = false);

/// <summary>Consumo do robô de atendimento (IA) no período: turnos respondidos, tokens e custo,
/// com quebra por assunto. Tokens e custo vêm <c>null</c> para quem não tem o módulo
/// <c>EstatisticaCustos</c> — tokens revelam custo, então saem junto.</summary>
public sealed record RoboConsumoDto(
    long Turnos,
    long? TokensEntrada,
    long? TokensSaida,
    long? TokensTotal,
    decimal? CustoUsd,
    IReadOnlyList<RoboConsumoAssuntoDto> PorAssunto);

/// <summary>Consumo do robô agrupado por assunto.</summary>
public sealed record RoboConsumoAssuntoDto(
    string Assunto,
    long Turnos,
    long? TokensEntrada,
    long? TokensSaida,
    long? TokensTotal,
    decimal? CustoUsd);

/// <summary>
/// ESTIMATIVA do custo Meta no período: templates enviados × tarifa da categoria cadastrada
/// (Mensageria → Regras). Utility dentro de janela de 24h aberta e texto de sessão não custam.
/// É estimativa: a fatura real vem da Meta.
/// </summary>
public sealed record CustosMetaDto(
    bool TarifaCadastrada,
    decimal TotalUsd,
    long TemplatesEnviados,
    long TemplatesCobrados,
    long TemplatesGratis,
    IReadOnlyList<CustoMetaTemplateDto> PorTemplate,
    IReadOnlyList<CustoMetaDiaDto> PorDia);

public sealed record CustoMetaTemplateDto(
    string Template, string Categoria, long Enviadas, long Cobradas, decimal? TarifaUsd, decimal TotalUsd);

public sealed record CustoMetaDiaDto(DateOnly Dia, long Enviadas, long Cobradas, decimal TotalUsd);

/// <summary>Cartões-resumo (KPIs) do período.</summary>
public sealed record EstatisticasResumoDto(
    long TotalMensagens,
    long Enviadas,
    long Recebidas,
    long TemplatesSistema,
    long TemplatesAtendente,
    long MensagensSessao,
    long ConversasNovas,
    int DiasNoPeriodo,
    double MediaDiaria,
    double TaxaEntrega,
    double TaxaLeitura,
    int Atendentes);

/// <summary>Ponto da série temporal (um dia): mensagens enviadas x recebidas.</summary>
public sealed record SerieDiaDto(DateOnly Dia, long Enviadas, long Recebidas);

/// <summary>Par rótulo→contagem (pizza/barras: categoria, template, status, atendente).</summary>
public sealed record RotuloContagemDto(string Rotulo, long Total);
