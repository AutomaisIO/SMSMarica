using System.Numerics;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;

namespace SMSMarica.Core.SolicitacoesExame.Identificadores;

public sealed class GeradorIdentificadores(SmsMaricaDbContext db) : IGeradorIdentificadores
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<string> ProximoAccessionAsync(CancellationToken cancellationToken = default)
    {
        var ano = DateTime.UtcNow.Year;
        var anoStr = ano.ToString();

        // AccessionNumber = {ano}{seq6} = 10 chars (limite aceito pelo equipamento Fuji).
        // Considera também o formato legado "SMS{ano}{seq6}" para não reiniciar a sequência.
        var doAno = await _db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.AccessionNumber.StartsWith(anoStr) || s.AccessionNumber.StartsWith("SMS" + anoStr))
            .Select(s => s.AccessionNumber)
            .ToListAsync(cancellationToken);

        var seq = 0;
        foreach (var acc in doAno)
        {
            if (acc.Length >= 6 && int.TryParse(acc[^6..], out var n) && n > seq)
            {
                seq = n;
            }
        }

        return $"{anoStr}{seq + 1:D6}";
    }

    public string NovoStudyInstanceUid()
    {
        // DICOM "2.25." + integer derivado do GUID (PS3.5 Annex B.2).
        var bigint = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true);
        return "2.25." + bigint.ToString();
    }
}
