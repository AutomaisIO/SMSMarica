using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Data;

namespace SMSMais.Core.Inteligencia;

/// <summary>
/// Serviço do módulo IA. Hoje só lista as bases ativas (usadas pela Consulta Inteligente e pela
/// tela de configuração). A antiga geração de SQL "perguntar" via API metrada da Anthropic foi
/// removida — a consulta conversável usa o motor local (feature consulta-inteligente /
/// IaChatController + aiengine no modo `dados`).
/// </summary>
public sealed class IaService(SmsMaisDbContext db) : IIaService
{
    public async Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivasAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.IaFontes
            .AsNoTracking()
            .Where(f => f.Ativo && f.ExcluidoEm == null)
            .OrderBy(f => f.Nome)
            .Select(f => new FonteResumoDto(
                f.Id, f.Nome, f.Tipo.ToString(), f.Ambiente.ToString(), f.Slug ?? string.Empty))
            .ToListAsync(cancellationToken);
    }
}
