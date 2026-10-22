using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMais.Core.Exames.ImagensInvalidadas;

/// <summary>
/// As imagens do estudo que NÃO vão ao paciente (invalidação vigente). Consulta única para todo
/// lugar que monta o que o paciente recebe — os PDFs de exame completo e de imagens — de modo que
/// nenhum deles esqueça o filtro.
/// </summary>
public static class SopsInvalidados
{
    public static async Task<IReadOnlyCollection<string>> DoEstudoAsync(
        SmsMaisDbContext db, string studyInstanceUID, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID)) return [];
        var uid = studyInstanceUID.Trim();
        return await db.ImagensInvalidadas.AsNoTracking()
            .Where(i => i.StudyInstanceUID == uid && i.ExcluidoEm == null)
            .Select(i => i.SopInstanceUID)
            .ToListAsync(ct);
    }
}
