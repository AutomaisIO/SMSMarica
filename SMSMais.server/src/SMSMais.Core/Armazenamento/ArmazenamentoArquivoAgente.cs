using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Armazenamento;

/// <summary>
/// Guarda, por pouco tempo, o arquivo (imagem/PDF) que o operador manda ao Agente IA pelo WhatsApp,
/// para o motor (Claude Code, mesmo host) ler e interpretar. Fica num diretório local em /tmp
/// (compartilhado entre os serviços — sem PrivateTmp), rotacionado por idade e tamanho: o arquivo
/// some sozinho depois que o turno já o leu. NÃO é o acervo do paciente (ADR-0066).
/// </summary>
public interface IArmazenamentoArquivoAgente
{
    /// <summary>Grava o conteúdo e devolve o CAMINHO ABSOLUTO (para o motor ler via ferramenta de leitura).</summary>
    Task<string> SalvarAsync(byte[] conteudo, string extensao, CancellationToken ct = default);
}

public sealed class ArmazenamentoArquivoAgente : IArmazenamentoArquivoAgente
{
    private readonly string _diretorio;
    private readonly TimeSpan _ttl;
    private readonly long _tetoBytes;
    private readonly ILogger<ArmazenamentoArquivoAgente> _logger;

    public ArmazenamentoArquivoAgente(IConfiguration configuration, ILogger<ArmazenamentoArquivoAgente> logger)
    {
        _logger = logger;
        _diretorio = configuration["AgenteArquivos:Diretorio"]
            ?? Path.Combine(Path.GetTempPath(), "smsmarica-agente-arquivos");
        // Folga generosa: o turno precisa achar o arquivo mesmo que demore a ser processado.
        _ttl = TimeSpan.FromHours(configuration.GetValue("AgenteArquivos:TtlHoras", 6));
        _tetoBytes = configuration.GetValue("AgenteArquivos:TamanhoMaximoMb", 500) * 1024L * 1024L;
    }

    public async Task<string> SalvarAsync(byte[] conteudo, string extensao, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_diretorio);
        var ext = LimparExtensao(extensao);
        var caminho = Path.Combine(_diretorio, $"{Guid.NewGuid():N}{ext}");
        await File.WriteAllBytesAsync(caminho, conteudo, ct);
        try { Rotacionar(); } catch (Exception ex) { _logger.LogWarning(ex, "Falha ao rotacionar arquivos do agente."); }
        return caminho;
    }

    // Só uma extensão curta e segura (a origem é o mime, não entrada livre); nunca separadores.
    private static string LimparExtensao(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return "";
        var e = ext.Trim().TrimStart('.');
        if (e.Length is 0 or > 5 || !e.All(char.IsLetterOrDigit)) return "";
        return "." + e.ToLowerInvariant();
    }

    private void Rotacionar()
    {
        if (!Directory.Exists(_diretorio)) return;
        var arquivos = new DirectoryInfo(_diretorio).GetFiles()
            .OrderBy(f => f.LastWriteTimeUtc).ToList();
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
