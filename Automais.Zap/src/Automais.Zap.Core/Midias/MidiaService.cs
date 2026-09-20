using System.Security.Cryptography;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Core.Midias;

/// <summary>Metadados de um arquivo hospedado. <see cref="Caminho"/> é relativo — quem responde
/// a requisição sabe o host e monta a URL absoluta.</summary>
public sealed record MidiaGuardada(
    Guid Id, string NomeArquivo, string MimeType, long TamanhoBytes,
    int? Largura, int? Altura, DateTimeOffset CriadoEm)
{
    public string Caminho => $"/midias/{Id}";
}

public sealed record MidiaConteudo(byte[] Conteudo, string MimeType, string NomeArquivo);

public interface IMidiaService
{
    /// <summary>
    /// Guarda um arquivo para o tenant. Mesmo conteúdo já guardado devolve o registro existente,
    /// e portanto a mesma URL — trocar a arte por uma igual não cria lixo nem muda endereço.
    /// </summary>
    Task<(MidiaGuardada? Midia, string? Erro)> GuardarAsync(
        Guid tenantId, string nomeArquivo, string mimeType, byte[] conteudo,
        string? categoria, Guid? usuarioId, CancellationToken ct = default);

    /// <summary>Binário para servir. Sem tenant: a URL é pública por definição — a Meta baixa
    /// sem credencial nenhuma.</summary>
    Task<MidiaConteudo?> ObterConteudoAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<MidiaGuardada>> ListarAsync(
        Guid tenantId, string? categoria = null, CancellationToken ct = default);

    /// <summary>Apaga. Só apaga do próprio tenant — id de outro devolve false, não 403 com pista.</summary>
    Task<bool> ApagarAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Arquivos que a plataforma hospeda para a Meta baixar. Hoje serve a arte de cabeçalho dos
/// modelos com foto no topo, que a Meta rebaixa a cada mensagem enviada.
/// </summary>
public sealed class MidiaService(ZapDbContext db, TimeProvider relogio) : IMidiaService
{
    /// <summary>Teto da Meta para imagem de cabeçalho.</summary>
    public const long TamanhoMaximoBytes = 5 * 1024 * 1024;

    /// <summary>A Meta só aceita PNG e JPEG em cabeçalho de modelo — aceitar mais aqui só adiaria
    /// a recusa para a hora do envio.</summary>
    private static readonly HashSet<string> MimesPermitidos =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg" };

    public async Task<(MidiaGuardada? Midia, string? Erro)> GuardarAsync(
        Guid tenantId, string nomeArquivo, string mimeType, byte[] conteudo,
        string? categoria, Guid? usuarioId, CancellationToken ct = default)
    {
        if (conteudo is null || conteudo.Length == 0) return (null, "Arquivo vazio.");
        if (conteudo.Length > TamanhoMaximoBytes)
            return (null, $"Arquivo maior que o limite da Meta ({TamanhoMaximoBytes / (1024 * 1024)} MB).");

        var mime = NormalizarMime(mimeType, nomeArquivo);
        if (!MimesPermitidos.Contains(mime))
            return (null, "A Meta só aceita PNG ou JPEG no cabeçalho de um modelo.");

        var hash = Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant();

        var existente = await db.Midias.AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.HashSha256 == hash, ct);
        if (existente is not null) return (Mapear(existente), null);

        var (largura, altura) = Dimensoes(conteudo, mime);
        var midia = new Midia
        {
            TenantId = tenantId,
            NomeArquivo = string.IsNullOrWhiteSpace(nomeArquivo) ? "arte" : nomeArquivo.Trim(),
            MimeType = mime,
            Conteudo = conteudo,
            TamanhoBytes = conteudo.Length,
            Largura = largura,
            Altura = altura,
            HashSha256 = hash,
            Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim(),
            CriadoPorUsuarioId = usuarioId,
            CriadoEm = relogio.GetUtcNow(),
        };

        db.Midias.Add(midia);
        await db.SaveChangesAsync(ct);
        return (Mapear(midia), null);
    }

    public async Task<MidiaConteudo?> ObterConteudoAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Midias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return m is null ? null : new MidiaConteudo(m.Conteudo, m.MimeType, m.NomeArquivo);
    }

    public async Task<IReadOnlyList<MidiaGuardada>> ListarAsync(
        Guid tenantId, string? categoria = null, CancellationToken ct = default)
    {
        var consulta = db.Midias.AsNoTracking().Where(m => m.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(categoria)) consulta = consulta.Where(m => m.Categoria == categoria);

        // Sem o Select, cada linha traria o binário inteiro só para listar nome e tamanho.
        return await consulta
            .OrderByDescending(m => m.CriadoEm)
            .Select(m => new MidiaGuardada(
                m.Id, m.NomeArquivo, m.MimeType, m.TamanhoBytes, m.Largura, m.Altura, m.CriadoEm))
            .ToListAsync(ct);
    }

    public async Task<bool> ApagarAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var apagadas = await db.Midias
            .Where(m => m.Id == id && m.TenantId == tenantId)
            .ExecuteDeleteAsync(ct);
        return apagadas > 0;
    }

    private static MidiaGuardada Mapear(Midia m) => new(
        m.Id, m.NomeArquivo, m.MimeType, m.TamanhoBytes, m.Largura, m.Altura, m.CriadoEm);

    /// <summary>
    /// O navegador nem sempre manda o tipo certo (<c>application/octet-stream</c> acontece), então
    /// a extensão desempata. Recusar um PNG legítimo por causa do cabeçalho do upload seria
    /// devolver um erro que o operador não tem como entender.
    /// </summary>
    private static string NormalizarMime(string? mimeType, string nomeArquivo)
    {
        var mime = (mimeType ?? "").Trim().ToLowerInvariant();
        if (MimesPermitidos.Contains(mime)) return mime;

        var ext = Path.GetExtension(nomeArquivo ?? "").ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => mime,
        };
    }

    /// <summary>
    /// Largura e altura lidas do cabeçalho do arquivo, sem decodificar a imagem (e sem dependência
    /// de biblioteca gráfica). Serve para a tela avisar que a arte está fora do formato deitado
    /// que o WhatsApp mostra no cabeçalho.
    /// </summary>
    private static (int? Largura, int? Altura) Dimensoes(byte[] b, string mime)
    {
        try
        {
            if (mime == "image/png" && b.Length > 24
                && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                return (LerBigEndian(b, 16), LerBigEndian(b, 20));
            }

            if (mime == "image/jpeg" && b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                // Percorre os marcadores até um SOF (0xC0–0xCF, fora de C4/C8/CC), que carrega
                // altura e largura logo depois do tamanho do segmento.
                var i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }

                    var marcador = b[i + 1];
                    if (marcador is >= 0xC0 and <= 0xCF && marcador is not (0xC4 or 0xC8 or 0xCC))
                        return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);

                    var tamanho = (b[i + 2] << 8) | b[i + 3];
                    if (tamanho <= 0) break;
                    i += 2 + tamanho;
                }
            }
        }
        catch (Exception)
        {
            // Dimensão é conforto de tela; arquivo estranho não pode impedir o upload.
        }

        return (null, null);
    }

    private static int LerBigEndian(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
}
