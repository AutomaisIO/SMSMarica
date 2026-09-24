using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMSMais.Core.Laudos.Assinatura.Nuvem;

/// <summary>Monta a cadeia ICP-Brasil de um certificado de titular.</summary>
public interface ICadeiaIcpBrasil
{
    /// <summary>
    /// Cadeia em DER, do titular até a raiz. Se o pacote do ITI não puder ser obtido, devolve
    /// só o titular (e loga): o PDF continua assinado, mas o validador pode pedir a cadeia.
    /// </summary>
    Task<IReadOnlyList<byte[]>> MontarAsync(byte[] certificadoTitular, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cadeia ICP-Brasil a partir do pacote oficial de ACs do ITI (ADR-0061). No modo Desktop a
/// cadeia vem da loja do Windows, que o VIDaaS Connect abastece; no servidor Linux não há loja,
/// e a credencial da IntegraICP traz só o certificado do médico. Seguir AIA não resolve porque
/// a AC VALID RFB v5 não publica a extensão (spike de 04/08/2026). O pacote tem ~176 certificados
/// (~350 KB) e é baixado uma vez por processo.
/// </summary>
public sealed class CadeiaIcpBrasil(
    IHttpClientFactory httpFactory,
    IOptions<IntegraIcpOptions> options,
    ILogger<CadeiaIcpBrasil> logger) : ICadeiaIcpBrasil
{
    public const string NomeHttpClient = "cadeia-icp-brasil";

    private readonly SemaphoreSlim _trava = new(1, 1);
    private (X509Certificate2Collection Raizes, X509Certificate2Collection Intermediarias)? _pacote;

    public async Task<IReadOnlyList<byte[]>> MontarAsync(byte[] certificadoTitular, CancellationToken cancellationToken = default)
    {
        using var titular = X509CertificateLoader.LoadCertificate(certificadoTitular);
        var pacote = await ObterPacoteAsync(cancellationToken);
        if (pacote is null) return [certificadoTitular];

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(pacote.Value.Raizes);
        chain.ChainPolicy.ExtraStore.AddRange(pacote.Value.Intermediarias);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        // Montar a cadeia não é validar o certificado: validade/revogação ficam com o PSC,
        // que só autoriza credencial de certificado ativo.
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

        if (!chain.Build(titular))
        {
            var status = string.Join(", ", chain.ChainStatus.Select(s => s.Status));
            logger.LogWarning("Cadeia ICP-Brasil: não fechou para '{Titular}' ({Status}); segue só com o titular.",
                titular.GetNameInfo(X509NameType.SimpleName, false), status);
            return [certificadoTitular];
        }

        return [.. chain.ChainElements.Select(e => e.Certificate.RawData)];
    }

    private async Task<(X509Certificate2Collection Raizes, X509Certificate2Collection Intermediarias)?> ObterPacoteAsync(
        CancellationToken ct)
    {
        if (_pacote is not null) return _pacote;
        await _trava.WaitAsync(ct);
        try
        {
            if (_pacote is not null) return _pacote;

            var url = options.Value.UrlCadeiaIcpBrasil;
            byte[] zip;
            try
            {
                zip = await httpFactory.CreateClient(NomeHttpClient).GetByteArrayAsync(url, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "Cadeia ICP-Brasil: não foi possível baixar o pacote do ITI ({Url}).", url);
                return null;
            }

            var raizes = new X509Certificate2Collection();
            var intermediarias = new X509Certificate2Collection();
            using (var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            {
                foreach (var entrada in arquivo.Entries.Where(e => e.Length > 0))
                {
                    try
                    {
                        using var ms = new MemoryStream();
                        await using (var s = entrada.Open()) await s.CopyToAsync(ms, ct);
                        var cert = CarregarDerOuPem(ms.ToArray());
                        if (cert is null) continue;
                        (cert.SubjectName.RawData.AsSpan().SequenceEqual(cert.IssuerName.RawData) ? raizes : intermediarias).Add(cert);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogDebug(ex, "Cadeia ICP-Brasil: entrada ignorada ({Nome}).", entrada.FullName);
                    }
                }
            }

            logger.LogInformation("Cadeia ICP-Brasil: pacote do ITI carregado ({Raizes} raízes, {Intermediarias} intermediárias).",
                raizes.Count, intermediarias.Count);
            _pacote = (raizes, intermediarias);
            return _pacote;
        }
        finally
        {
            _trava.Release();
        }
    }

    private static X509Certificate2? CarregarDerOuPem(byte[] bytes)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(bytes);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            var texto = System.Text.Encoding.ASCII.GetString(bytes);
            if (!texto.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal)) return null;
            return X509Certificate2.CreateFromPem(texto);
        }
    }
}
