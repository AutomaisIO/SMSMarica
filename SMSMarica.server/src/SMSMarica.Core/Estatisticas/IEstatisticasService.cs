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
}
