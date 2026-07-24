namespace SMSMarica.Core.Estatisticas.Dtos;

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
    IReadOnlyList<RotuloContagemDto> PorAtendente);

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
