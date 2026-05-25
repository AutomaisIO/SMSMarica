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
        var prefixo = $"SMS{ano}";

        // Próxima sequência baseada no maior já gravado para o ano.
        var ultimo = await _db.SolicitacoesExame.AsNoTracking()
            .Where(s => s.AccessionNumber.StartsWith(prefixo))
            .OrderByDescending(s => s.AccessionNumber)
            .Select(s => s.AccessionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var seq = 1;
        if (!string.IsNullOrEmpty(ultimo) && ultimo.Length == prefixo.Length + 6
            && int.TryParse(ultimo[prefixo.Length..], out var n))
        {
            seq = n + 1;
        }

        return $"{prefixo}{seq:D6}";
    }

    public string NovoStudyInstanceUid()
    {
        // DICOM "2.25." + integer derivado do GUID (PS3.5 Annex B.2).
        var bigint = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true);
        return "2.25." + bigint.ToString();
    }
}
