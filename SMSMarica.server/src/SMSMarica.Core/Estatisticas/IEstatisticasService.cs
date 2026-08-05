using SMSMarica.Core.Estatisticas.Dtos;

namespace SMSMarica.Core.Estatisticas;

/// <summary>
/// Agregações gerenciais das comunicações WhatsApp (Central de Atendimento + envios automáticos).
/// Só leitura: nunca retorna conteúdo de mensagem nem identidade de paciente.
/// </summary>
public interface IEstatisticasService
{
    /// <summary>
    /// Retrato do WhatsApp no intervalo [de, ate] (inclusive nas duas pontas, por dia).
    /// Exclui mensagens em modo simulado.
    /// </summary>
    Task<EstatisticasWhatsAppDto> ObterWhatsAppAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>
    /// Agregados dos exames de imagem no intervalo [de, ate] (âncora <c>dataRef</c>, ver
    /// <see cref="Dtos.EstatisticasExamesImagemDto"/>), no escopo de unidade do usuário atual e,
    /// opcionalmente, restrito a uma única unidade EXECUTANTE. Só contagens/médias — sem PII.
    /// </summary>
    Task<EstatisticasExamesImagemDto> ObterExamesImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, CancellationToken ct = default);

    /// <summary>
    /// Lista ANALÍTICA (uma linha por exame) que sustenta os agregados de imagem, com PII de
    /// paciente, para exportação. Mesmo recorte/escopo de <see cref="ObterExamesImagemAsync"/>.
    /// Registra auditoria de quem exportou. Deve ser chamado apenas por endpoint com gate de PII.
    /// </summary>
    Task<IReadOnlyList<ExameImagemAnaliticoDto>> ListarAnaliticoExamesImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, CancellationToken ct = default);
}
