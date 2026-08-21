using Automais.Zap.Core.Seguranca;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Automais.Zap.Core.Meta;

/// <summary>Credenciais já decifradas, prontas para uso.</summary>
public sealed record CredenciaisMeta(string? AppId, string? AppSecret, string? VerifyToken, string? TokenSistema, string BaseUrl)
{
    /// <summary>O webhook precisa disto para conferir HMAC. Sem, falha fechado.</summary>
    public bool PodeReceberWebhook => !string.IsNullOrWhiteSpace(AppSecret);

    /// <summary>O console precisa de token + app id para falar com a Graph API.</summary>
    public bool PodeGerenciar => !string.IsNullOrWhiteSpace(TokenSistema) && !string.IsNullOrWhiteSpace(AppId);

    /// <summary>Token de app (<c>{app-id}|{app-secret}</c>) — exigido pelos endpoints do próprio App.</summary>
    public string? TokenApp => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret)
        ? $"{AppId}|{AppSecret}"
        : null;
}

/// <summary>Dados que a tela envia. Campo nulo = "não mexer"; string vazia = "apagar".</summary>
public sealed record AtualizarConfiguracaoMeta(
    string? AppId,
    string? AppSecret,
    string? VerifyToken,
    string? TokenSistema,
    string? BaseUrl);

public interface IConfiguracaoMetaService
{
    Task<CredenciaisMeta> ObterAsync(CancellationToken ct = default);
    Task<ConfiguracaoMeta> ObterBrutaAsync(CancellationToken ct = default);
    Task SalvarAsync(AtualizarConfiguracaoMeta dados, CancellationToken ct = default);
}

public sealed class ConfiguracaoMetaService(
    ZapDbContext db,
    IProtetorSegredos protetor,
    IOptions<MetaOptions> env,
    IMemoryCache cache,
    TimeProvider relogio) : IConfiguracaoMetaService
{
    private const string ChaveCache = "meta:credenciais";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<CredenciaisMeta> ObterAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue<CredenciaisMeta>(ChaveCache, out var cacheado) && cacheado is not null)
        {
            return cacheado;
        }

        var linha = await db.ConfiguracoesMeta.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ConfiguracaoMeta.IdSingleton, ct);

        // O env é o bootstrap: enquanto a tela não for salva, vale o que veio de Meta__*.
        // Também é a rede de segurança se alguém salvar a tela pela metade.
        var creds = new CredenciaisMeta(
            AppId: OuEnv(linha?.AppId, env.Value.AppId),
            AppSecret: OuEnv(protetor.Revelar(linha?.AppSecretCifrado), env.Value.AppSecret),
            VerifyToken: OuEnv(protetor.Revelar(linha?.VerifyTokenCifrado), env.Value.VerifyToken),
            TokenSistema: protetor.Revelar(linha?.TokenSistemaCifrado),
            BaseUrl: string.IsNullOrWhiteSpace(linha?.BaseUrl) ? "https://graph.facebook.com/v21.0/" : linha.BaseUrl);

        cache.Set(ChaveCache, creds, Ttl);
        return creds;
    }

    public async Task<ConfiguracaoMeta> ObterBrutaAsync(CancellationToken ct = default)
        => await db.ConfiguracoesMeta.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ConfiguracaoMeta.IdSingleton, ct)
           ?? new ConfiguracaoMeta();

    public async Task SalvarAsync(AtualizarConfiguracaoMeta dados, CancellationToken ct = default)
    {
        var linha = await db.ConfiguracoesMeta.FirstOrDefaultAsync(x => x.Id == ConfiguracaoMeta.IdSingleton, ct);
        if (linha is null)
        {
            linha = new ConfiguracaoMeta();
            db.ConfiguracoesMeta.Add(linha);
        }

        if (dados.AppId is not null) linha.AppId = Vazio(dados.AppId);
        if (dados.BaseUrl is not null && !string.IsNullOrWhiteSpace(dados.BaseUrl)) linha.BaseUrl = dados.BaseUrl.Trim();

        // Campo de senha em branco na tela significa "mantém o que está lá", não "apaga".
        // Apagar de verdade é enviar o literal "-".
        AplicarSegredo(dados.AppSecret, v => linha.AppSecretCifrado = v);
        AplicarSegredo(dados.VerifyToken, v => linha.VerifyTokenCifrado = v);
        AplicarSegredo(dados.TokenSistema, v => linha.TokenSistemaCifrado = v);

        linha.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        cache.Remove(ChaveCache);
    }

    private void AplicarSegredo(string? entrada, Action<string?> definir)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return;
        if (entrada.Trim() == "-") { definir(null); return; }
        definir(protetor.Proteger(entrada.Trim()));
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? OuEnv(string? doBanco, string? doEnv)
        => !string.IsNullOrWhiteSpace(doBanco) ? doBanco : (string.IsNullOrWhiteSpace(doEnv) ? null : doEnv);
}
