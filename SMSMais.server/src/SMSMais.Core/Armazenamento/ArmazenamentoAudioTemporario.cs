using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Armazenamento;

/// <summary>
/// Guarda, por pouco tempo, o áudio (TTS) que o Agente IA devolve pelo WhatsApp. A Meta precisa de
/// uma URL pública para buscar o arquivo; servimos por <c>GET /publico/audio-agente/{token}</c> só o
/// tempo da entrega. NÃO vai para o S3: fica num diretório local rotacionado por idade e tamanho —
/// o arquivo some sozinho.
/// </summary>
public interface IArmazenamentoAudioTemporario
{
    /// <summary>Grava o áudio e devolve o token (nome do arquivo) para montar o link público.</summary>
    Task<string> GuardarAsync(byte[] audio, CancellationToken ct = default);

    /// <summary>Lê o áudio pelo token, ou null se não existe/expirou. Token inválido = null.</summary>
    Task<byte[]?> LerAsync(string token, CancellationToken ct = default);
}

public sealed partial class ArmazenamentoAudioTemporario : IArmazenamentoAudioTemporario
{
    private readonly string _diretorio;
    private readonly TimeSpan _ttl;
    private readonly long _tetoBytes;
    private readonly ILogger<ArmazenamentoAudioTemporario> _logger;

    public ArmazenamentoAudioTemporario(IConfiguration configuration, ILogger<ArmazenamentoAudioTemporario> logger)
    {
        _logger = logger;
        _diretorio = configuration["AudioAgente:Diretorio"]
            ?? Path.Combine(Path.GetTempPath(), "smsmarica-audio-agente");
        _ttl = TimeSpan.FromMinutes(configuration.GetValue("AudioAgente:TtlMinutos", 15));
        _tetoBytes = configuration.GetValue("AudioAgente:TamanhoMaximoMb", 200) * 1024L * 1024L;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TokenValido();

    public async Task<string> GuardarAsync(byte[] audio, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_diretorio);
        var token = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(Caminho(token), audio, ct);
        try { Rotacionar(); } catch (Exception ex) { _logger.LogWarning(ex, "Falha ao rotacionar o diretório de áudio temporário."); }
        return token;
    }

    public async Task<byte[]?> LerAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || !TokenValido().IsMatch(token)) return null;
        var caminho = Caminho(token);
        if (!File.Exists(caminho)) return null;
        return await File.ReadAllBytesAsync(caminho, ct);
    }

    private string Caminho(string token) => Path.Combine(_diretorio, token + ".ogg");

    /// <summary>Apaga o que passou do TTL e, se ainda estourar o teto, os mais antigos até caber.</summary>
    private void Rotacionar()
    {
        if (!Directory.Exists(_diretorio)) return;
        var arquivos = new DirectoryInfo(_diretorio).GetFiles("*.ogg")
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
            try { var tam = f.Length; f.Delete(); total -= tam; } catch { /* best-effort */ }
        }
    }
}
