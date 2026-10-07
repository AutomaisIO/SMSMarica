using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Armazenamento;

/// <summary>Conteúdo servido publicamente (bytes + tipo), ou null se não existe/expirou.</summary>
public sealed record DocumentoPublico(byte[] Conteudo, string ContentType);

/// <summary>
/// Guarda, por pouco tempo, um arquivo que o Agente IA gera para ENTREGAR ao operador no WhatsApp
/// (PDF, planilha, etc.). A Meta busca o link; servimos por <c>GET /publico/arquivo-agente/{token}</c>
/// só o tempo da entrega. Diretório local rotacionado por idade/tamanho — some sozinho.
/// </summary>
public interface IArmazenamentoDocumentoPublico
{
    /// <summary>Grava os bytes com a extensão dada e devolve o token para montar o link público.</summary>
    Task<string> GuardarAsync(byte[] conteudo, string extensao, CancellationToken ct = default);

    /// <summary>Lê pelo token (bytes + content-type). Token inválido/arquivo ausente = null.</summary>
    Task<DocumentoPublico?> LerAsync(string token, CancellationToken ct = default);
}

public sealed partial class ArmazenamentoDocumentoPublico : IArmazenamentoDocumentoPublico
{
    private readonly string _diretorio;
    private readonly TimeSpan _ttl;
    private readonly long _tetoBytes;
    private readonly ILogger<ArmazenamentoDocumentoPublico> _logger;

    public ArmazenamentoDocumentoPublico(IConfiguration configuration, ILogger<ArmazenamentoDocumentoPublico> logger)
    {
        _logger = logger;
        _diretorio = configuration["ArquivoPublico:Diretorio"]
            ?? Path.Combine(Path.GetTempPath(), "smsmarica-arquivo-publico");
        _ttl = TimeSpan.FromMinutes(configuration.GetValue("ArquivoPublico:TtlMinutos", 30));
        _tetoBytes = configuration.GetValue("ArquivoPublico:TamanhoMaximoMb", 500) * 1024L * 1024L;
    }

    [GeneratedRegex("^[0-9a-f]{32}(\\.[a-z0-9]{1,5})?$")]
    private static partial Regex TokenValido();

    public async Task<string> GuardarAsync(byte[] conteudo, string extensao, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_diretorio);
        var ext = LimparExtensao(extensao);
        var token = $"{Guid.NewGuid():N}{ext}";
        await File.WriteAllBytesAsync(Path.Combine(_diretorio, token), conteudo, ct);
        try { Rotacionar(); } catch (Exception ex) { _logger.LogWarning(ex, "Falha ao rotacionar arquivos públicos."); }
        return token;
    }

    public async Task<DocumentoPublico?> LerAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || !TokenValido().IsMatch(token)) return null;
        var caminho = Path.Combine(_diretorio, token);
        if (!File.Exists(caminho)) return null;
        var bytes = await File.ReadAllBytesAsync(caminho, ct);
        return new DocumentoPublico(bytes, ContentType(Path.GetExtension(token)));
    }

    private static string LimparExtensao(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return "";
        var e = ext.Trim().TrimStart('.');
        if (e.Length is 0 or > 5 || !e.All(char.IsLetterOrDigit)) return "";
        return "." + e.ToLowerInvariant();
    }

    private static string ContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls" => "application/vnd.ms-excel",
        ".csv" => "text/csv",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".txt" => "text/plain",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream",
    };

    private void Rotacionar()
    {
        if (!Directory.Exists(_diretorio)) return;
        var arquivos = new DirectoryInfo(_diretorio).GetFiles().OrderBy(f => f.LastWriteTimeUtc).ToList();
        var agora = DateTime.UtcNow;
        foreach (var f in arquivos.ToList())
        {
            if (agora - f.LastWriteTimeUtc <= _ttl) continue;
            try { f.Delete(); arquivos.Remove(f); } catch { /* best-effort */ }
        }
        var total = arquivos.Sum(f => f.Length);
        foreach (var f in arquivos)
        {
            if (total <= _tetoBytes) break;
            try { var t = f.Length; f.Delete(); total -= t; } catch { /* best-effort */ }
        }
    }
}
