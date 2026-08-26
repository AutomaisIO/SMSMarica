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
    RoboConsumoDto Robo);

/// <summary>Consumo do robô de atendimento (IA) no período: turnos respondidos, tokens e custo,
/// com quebra por assunto. Alimenta a seção de custo do robô no relatório de mensagens.</summary>
public sealed record RoboConsumoDto(
    long Turnos,
    long TokensEntrada,
    long TokensSaida,
    long TokensTotal,
    decimal CustoUsd,
    IReadOnlyList<RoboConsumoAssuntoDto> PorAssunto);

/// <summary>Consumo do robô agrupado por assunto.</summary>
public sealed record RoboConsumoAssuntoDto(
    string Assunto,
    long Turnos,
    long TokensEntrada,
    long TokensSaida,
    long TokensTotal,
    decimal CustoUsd);

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
