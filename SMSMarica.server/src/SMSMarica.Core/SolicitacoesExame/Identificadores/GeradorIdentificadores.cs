using System.Numerics;
using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMarica.Core.SolicitacoesExame.Identificadores;

public sealed class GeradorIdentificadores(SmsMaisDbContext db) : IGeradorIdentificadores
{
    private readonly SmsMaisDbContext _db = db;

    public async Task<string> ProximoAccessionAsync(CancellationToken cancellationToken = default)
    {
        // Data de Maricá (UTC-3 fixo — Brasil sem horário de verão desde 2019).
        var hoje = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).Date;
        var prefixo = hoje.ToString("yyMMdd"); // ex.: 260624

        // AccessionNumber = {AAMMDD}{seq} — a sequência reinicia a cada dia
        // (ex.: 260624019 = 19º exame de 24/06/2026). Mínimo 3 dígitos; expande
        // sozinha se passar de 999 no mesmo dia. Total <= 16 chars (limite Fuji SH).
        var doDia = await _db.ExamesImagem.AsNoTracking()
            .Where(s => s.AccessionNumber.StartsWith(prefixo))
            .Select(s => s.AccessionNumber)
            .ToListAsync(cancellationToken);

        var seq = 0;
        foreach (var acc in doDia)
        {
            if (acc.Length > 6 && int.TryParse(acc[6..], out var n) && n > seq)
            {
                seq = n;
            }
        }

        return $"{prefixo}{seq + 1:D3}";
    }

    public string NovoStudyInstanceUid()
    {
        // DICOM "2.25." + integer derivado do GUID (PS3.5 Annex B.2).
        var bigint = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true);
        return "2.25." + bigint.ToString();
    }
}
