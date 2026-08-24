using SMSMais.Core.Inteligencia.Dtos;

namespace SMSMais.Core.Inteligencia;

/// <summary>
/// Serviço do módulo IA. Lista as bases ativas usadas pela Consulta Inteligente e pela
/// configuração. A geração de SQL "perguntar" (via API metrada) foi removida — a consulta
/// conversável passou para o motor local (feature consulta-inteligente / IaChatController).
/// </summary>
public interface IIaService
{
    Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivasAsync(CancellationToken cancellationToken = default);
}
