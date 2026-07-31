using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Geo.Google;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Geo;

namespace SMSMarica.Core.Geo;

public sealed class GeocodificadorService(
    SmsMaricaDbContext db,
    IGoogleGeocodingClient google,
    ILogger<GeocodificadorService> logger) : IGeocodificadorService
{
    public async Task<Coordenada?> GeocodificarAsync(Endereco? endereco, CancellationToken ct = default)
    {
        var normalizado = Normalizar(endereco);
        if (normalizado is null) return null;

        var hash = Hash(normalizado);
        var existente = await db.GeoEnderecos.FirstOrDefaultAsync(g => g.Hash == hash, ct);

        // Cache válido (coordenada confiável já resolvida).
        if (existente is not null && !existente.RevisaoPendente && !(existente.Latitude == 0 && existente.Longitude == 0))
        {
            return new Coordenada(existente.Latitude, existente.Longitude);
        }
        // Já está na fila de revisão — não fica re-chamando o Google.
        if (existente is { RevisaoPendente: true })
        {
            return null;
        }

        try
        {
            var r = await google.GeocodificarAsync(normalizado, ct);
            if (r is { Ok: true })
            {
                var pendente = EhBaixaPrecisao(r.Precisao);
                Upsert(existente, hash, normalizado, r.Latitude, r.Longitude, FonteGeocodigo.Google, r.Precisao, pendente);
                await db.SaveChangesAsync(ct);
                return new Coordenada(r.Latitude, r.Longitude);
            }

            // Google não encontrou → registra para revisão manual (não bloqueia o chamador).
            Upsert(existente, hash, normalizado, 0, 0, FonteGeocodigo.Google, r?.Precisao, revisaoPendente: true);
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao geocodificar endereço; registrando para revisão.");
            Upsert(existente, hash, normalizado, 0, 0, FonteGeocodigo.Google, null, revisaoPendente: true);
            try { await db.SaveChangesAsync(ct); } catch { /* geocodificação é best-effort */ }
            return null;
        }
    }

    public async Task<IReadOnlyList<GeocodigoDto>> ListarRevisaoPendenteAsync(CancellationToken ct = default)
    {
        var lista = await db.GeoEnderecos.AsNoTracking()
            .Where(g => g.RevisaoPendente)
            .OrderBy(g => g.GeocodificadoEm)
            .ToListAsync(ct);
        return [.. lista.Select(ParaDto)];
    }

    public async Task FixarManualAsync(FixarGeocodigoRequest request, CancellationToken ct = default)
    {
        var g = await db.GeoEnderecos.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NaoEncontradoException(nameof(GeoEndereco), request.Id);

        g.Latitude = request.Latitude;
        g.Longitude = request.Longitude;
        g.Fonte = FonteGeocodigo.Manual;
        g.Precisao = "MANUAL";
        g.RevisaoPendente = false;
        g.GeocodificadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private void Upsert(
        GeoEndereco? existente, string hash, string normalizado,
        double lat, double lng, FonteGeocodigo fonte, string? precisao, bool revisaoPendente)
    {
        if (existente is null)
        {
            db.GeoEnderecos.Add(new GeoEndereco
            {
                Id = Guid.CreateVersion7(),
                Hash = hash,
                EnderecoNormalizado = normalizado,
                Latitude = lat,
                Longitude = lng,
                Fonte = fonte,
                Precisao = precisao,
                RevisaoPendente = revisaoPendente,
                GeocodificadoEm = DateTime.UtcNow,
            });
        }
        else
        {
            existente.Latitude = lat;
            existente.Longitude = lng;
            existente.Fonte = fonte;
            existente.Precisao = precisao;
            existente.RevisaoPendente = revisaoPendente;
            existente.GeocodificadoEm = DateTime.UtcNow;
        }
    }

    private static bool EhBaixaPrecisao(string? precisao) =>
        string.Equals(precisao, "APPROXIMATE", StringComparison.OrdinalIgnoreCase);

    private static string? Normalizar(Endereco? e)
    {
        if (e is null) return null;
        if (string.IsNullOrWhiteSpace(e.Logradouro) || string.IsNullOrWhiteSpace(e.Cidade)) return null;

        var partes = new[]
        {
            $"{e.Logradouro} {e.Numero}".Trim(),
            e.Bairro,
            e.Cidade,
            e.Uf,
            e.Cep,
        };
        var s = string.Join(", ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return RemoverAcentos(s).ToLowerInvariant().Trim();
    }

    private static string RemoverAcentos(string texto)
    {
        var d = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Hash(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    private static GeocodigoDto ParaDto(GeoEndereco g) => new(
        g.Id, g.EnderecoNormalizado, g.Latitude, g.Longitude,
        g.Fonte.ToString(), g.Precisao, g.RevisaoPendente, g.GeocodificadoEm);
}
