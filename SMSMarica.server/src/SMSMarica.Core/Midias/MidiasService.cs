using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Midias.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Midias;

public sealed class MidiasService(SmsMaisDbContext db) : IMidiasService
{
    private readonly SmsMaisDbContext _db = db;

    /// <summary>5 MB — suficiente para logos/timbres; evita estourar o bytea.</summary>
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> MimesPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml",
    };

    public async Task<MidiaDto> EnviarAsync(
        Guid? usuarioId,
        string nomeArquivo,
        string mimeType,
        byte[] conteudo,
        string? categoria,
        CancellationToken cancellationToken = default)
    {
        if (conteudo is null || conteudo.Length == 0)
        {
            throw new ValidacaoException("midia.vazia", "Arquivo vazio.");
        }
        if (conteudo.Length > TamanhoMaximoBytes)
        {
            throw new ValidacaoException("midia.muito_grande", $"Arquivo excede o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }
        if (!MimesPermitidos.Contains(mimeType))
        {
            throw new ValidacaoException("midia.tipo_invalido", $"Tipo '{mimeType}' não permitido. Use PNG, JPEG, GIF, WEBP ou SVG.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant();

        // Dedup: mesmo conteúdo já guardado → reaproveita.
        var existente = await _db.Midias.AsNoTracking()
            .FirstOrDefaultAsync(m => m.HashSha256 == hash, cancellationToken);
        if (existente is not null)
        {
            return ParaDto(existente);
        }

        var (largura, altura) = SniffDimensoes(conteudo, mimeType);

        var midia = new Midia
        {
            Id = Guid.CreateVersion7(),
            NomeArquivo = string.IsNullOrWhiteSpace(nomeArquivo) ? "arquivo" : nomeArquivo.Trim(),
            MimeType = mimeType,
            Conteudo = conteudo,
            TamanhoBytes = conteudo.Length,
            Largura = largura,
            Altura = altura,
            Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim(),
            HashSha256 = hash,
            CriadoPorUsuarioId = usuarioId,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Midias.Add(midia);
        await _db.SaveChangesAsync(cancellationToken);
        return ParaDto(midia);
    }

    public async Task<MidiaConteudo?> ObterConteudoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Midias.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return m is null ? null : new MidiaConteudo(m.Conteudo, m.MimeType, m.NomeArquivo);
    }

    private static MidiaDto ParaDto(Midia m) => new(
        m.Id, m.NomeArquivo, m.MimeType, m.TamanhoBytes, m.Largura, m.Altura, m.Categoria, m.CriadoEm);

    // ---- Sniff de dimensões (sem dependência de imagem; best-effort) ----

    private static (int? largura, int? altura) SniffDimensoes(byte[] b, string mime)
    {
        try
        {
            if (mime.Equals("image/png", StringComparison.OrdinalIgnoreCase) && b.Length >= 24)
            {
                // IHDR: largura/altura big-endian nos bytes 16..23.
                var w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                var h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return (w, h);
            }
            if (mime.Equals("image/gif", StringComparison.OrdinalIgnoreCase) && b.Length >= 10)
            {
                // Logical screen descriptor: largura/altura little-endian nos bytes 6..9.
                var w = b[6] | (b[7] << 8);
                var h = b[8] | (b[9] << 8);
                return (w, h);
            }
            if (mime.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return SniffJpeg(b);
            }
        }
        catch
        {
            // Best-effort: dimensão é opcional.
        }
        return (null, null);
    }

    private static (int? largura, int? altura) SniffJpeg(byte[] b)
    {
        var i = 2; // pula SOI (FF D8)
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15 carregam dimensões.
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                var h = (b[i + 5] << 8) | b[i + 6];
                var w = (b[i + 7] << 8) | b[i + 8];
                return (w, h);
            }
            var len = (b[i + 2] << 8) | b[i + 3];
            if (len <= 0) break;
            i += 2 + len;
        }
        return (null, null);
    }
}
