using System.Security.Cryptography;
using System.Text;
using Automais.Pabx.Api.Data;
using Automais.Pabx.Api.Data.Entities;
using Automais.Pabx.Api.Asterisk.Ami;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Automais.Pabx.Api.Asterisk;

/// <summary>
/// Gera a configuração SIP dos ramais gerenciados e aplica no Asterisk.
/// Interface plugável: chan_sip hoje (Asterisk 16 da FalarMais); PJSIP no futuro
/// (chan_sip foi removido no Asterisk 21) sem mexer no domínio.
/// </summary>
public interface IGeradorConfigSip
{
    /// <summary>Regenera o arquivo de ramais (com backup) e recarrega o SIP via AMI.</summary>
    Task<AplicacaoResultado> AplicarAsync(CancellationToken ct = default);
}

public sealed record AplicacaoResultado(int RamaisEscritos, string Arquivo, bool ReloadExecutado, string? BackupCriado);

public sealed class GeradorConfigChanSip(
    PabxDbContext db,
    IDataProtectionProvider dataProtection,
    IAmiClientFactory amiFactory,
    IOptions<AsteriskOptions> options,
    TimeProvider timeProvider,
    ILogger<GeradorConfigChanSip> logger) : IGeradorConfigSip
{
    public const string ProtetorSecret = "Automais.Pabx.Ramal.Secret";

    private readonly AsteriskOptions _opcoes = options.Value;

    public async Task<AplicacaoResultado> AplicarAsync(CancellationToken ct = default)
    {
        // Só os gerenciados: os adotados continuam vivendo no sip_custom.conf da FalarMais.
        var ramais = await db.Ramais
            .Where(r => r.Origem == OrigemRamal.Gerenciado && r.Ativo)
            .OrderBy(r => r.Numero)
            .ToListAsync(ct);

        var protetor = dataProtection.CreateProtector(ProtetorSecret);
        var conteudo = GerarConteudo(ramais, protetor);

        var backup = await ArquivoSeguro.EscreverComBackupAsync(
            _opcoes.SipConfPath, conteudo, _opcoes.BackupDir, timeProvider, ct);

        await RegistrarArquivoAsync(_opcoes.SipConfPath, conteudo, ct);

        var reload = false;
        if (amiFactory.Habilitado)
        {
            await using var ami = await amiFactory.ConectarAsync(ct);
            await ami.ExecutarAsync("Command", new Dictionary<string, string> { ["Command"] = "sip reload" }, ct);
            reload = true;
            logger.LogInformation("sip reload executado após regenerar {Arquivo} ({Qtde} ramais)", _opcoes.SipConfPath, ramais.Count);
        }
        else
        {
            logger.LogWarning("AMI desabilitado: {Arquivo} gerado sem reload", _opcoes.SipConfPath);
        }

        return new AplicacaoResultado(ramais.Count, _opcoes.SipConfPath, reload, backup);
    }

    private string GerarConteudo(IReadOnlyList<Ramal> ramais, IDataProtector protetor)
    {
        var sb = new StringBuilder();
        var agora = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), FusoBrasilia).ToString("yyyy-MM-dd HH:mm:ss");
        sb.AppendLine("; ============================================================================");
        sb.AppendLine("; RAMAIS SMS MARICA - GERADO AUTOMATICAMENTE PELO AUTOMAIS.PABX");
        sb.AppendLine("; NAO EDITAR A MAO: qualquer alteracao manual e sobrescrita na proxima geracao.");
        sb.AppendLine($"; Gerado em {agora} (Brasilia). Telefones chegam via WireGuard (10.200.<id>.x).");
        sb.AppendLine("; ============================================================================");
        sb.AppendLine();

        foreach (var ramal in ramais)
        {
            sb.Append('[').Append(ramal.Numero).AppendLine("]");
            sb.Append("secret=").AppendLine(protetor.Unprotect(ramal.SecretCifrado));
            sb.AppendLine("type=friend");
            sb.AppendLine("qualify=yes");
            sb.AppendLine("nat=force_rport,comedia");
            sb.AppendLine("call-limit=1");
            sb.AppendLine("host=dynamic");
            sb.AppendLine("disallow=all");
            sb.AppendLine("allow=alaw");
            sb.AppendLine("allow=ulaw");
            sb.AppendLine("allow=gsm");
            sb.AppendLine("context=PLANO");
            sb.Append("callerid=").AppendLine(string.IsNullOrWhiteSpace(ramal.CallerId) ? ramal.Numero : ramal.CallerId);
            sb.AppendLine("canreinvite=no");
            sb.AppendLine("rtptimeout=60");
            sb.AppendLine("rtpholdtimeout=180");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task RegistrarArquivoAsync(string caminho, string conteudo, CancellationToken ct)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo)));
        var registro = await db.ArquivosGerenciados.FirstOrDefaultAsync(a => a.Caminho == caminho, ct);
        if (registro is null)
        {
            registro = new ArquivoGerenciado { Caminho = caminho, HashSha256 = hash, Tipo = TipoArquivoGerenciado.ConfigSip };
            db.ArquivosGerenciados.Add(registro);
        }

        registro.HashSha256 = hash;
        registro.AtualizadoEm = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    private static readonly TimeZoneInfo FusoBrasilia =
        TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");
}

/// <summary>Escrita com backup timestampado — o servidor é compartilhado, nada se perde.</summary>
public static class ArquivoSeguro
{
    public static async Task<string?> EscreverComBackupAsync(
        string caminho, string conteudo, string backupDir, TimeProvider timeProvider, CancellationToken ct)
    {
        string? backup = null;
        if (File.Exists(caminho))
        {
            Directory.CreateDirectory(backupDir);
            var stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss");
            backup = Path.Combine(backupDir, $"{Path.GetFileName(caminho)}.{stamp}");
            File.Copy(caminho, backup, overwrite: true);
        }

        var dir = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(caminho, conteudo, ct);
        return backup;
    }
}
