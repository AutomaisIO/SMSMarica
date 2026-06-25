using System.Net;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;

namespace SMSMarica.Core.Armazenamento;

/// <summary>
/// Implementação DigitalOcean Spaces (S3) de <see cref="IArmazenamentoArquivos"/>.
/// As credenciais (Access/Secret Key) e o destino (endpoint/region/bucket) vêm do
/// provedor <c>digitalocean_spaces</c> em Integrações — cifrados em repouso via
/// <see cref="IProtetorSegredos"/>. Objetos privados; o download passa pelo backend
/// (<c>GET /anexos/{id}/conteudo</c>), nunca por URL pública.
/// </summary>
public sealed class ArmazenamentoSpaces : IArmazenamentoArquivos, IDisposable
{
    private const string ProvedorSpaces = "digitalocean_spaces";

    private readonly SmsMaricaDbContext _db;
    private readonly IProtetorSegredos _protetor;
    private readonly string _prefixo;

    private IAmazonS3? _cliente;
    private string? _bucket;

    public ArmazenamentoSpaces(SmsMaricaDbContext db, IProtetorSegredos protetor, IConfiguration configuration)
    {
        _db = db;
        _protetor = protetor;
        _prefixo = (configuration["Armazenamento:Prefixo"] ?? "arquivos").Trim('/');
    }

    public string MontarChaveDocumento(Guid pacienteId, Guid documentoId, string extensao = "pdf")
        => ChavesArmazenamento.Documento(_prefixo, pacienteId, documentoId, extensao);

    public async Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
    {
        var (cliente, bucket) = await GarantirClienteAsync(cancellationToken);
        using var stream = new MemoryStream(conteudo, writable: false);
        await cliente.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = chave,
                InputStream = stream,
                ContentType = "application/pdf",
            },
            cancellationToken);
    }

    public async Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default)
    {
        var (cliente, bucket) = await GarantirClienteAsync(cancellationToken);
        try
        {
            using var resposta = await cliente.GetObjectAsync(bucket, chave, cancellationToken);
            using var ms = new MemoryStream();
            await resposta.ResponseStream.CopyToAsync(ms, cancellationToken);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task ExcluirAsync(string chave, CancellationToken cancellationToken = default)
    {
        var (cliente, bucket) = await GarantirClienteAsync(cancellationToken);
        await cliente.DeleteObjectAsync(bucket, chave, cancellationToken);
    }

    /// <summary>Lê a credencial cifrada do provedor e constrói o cliente S3 sob demanda (1x por scope).</summary>
    private async Task<(IAmazonS3 Cliente, string Bucket)> GarantirClienteAsync(CancellationToken ct)
    {
        if (_cliente is not null && _bucket is not null) return (_cliente, _bucket);

        var cred = await _db.IntegracaoCredenciais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Provedor == ProvedorSpaces, ct)
            ?? throw new InvalidOperationException(
                "Armazenamento Spaces: credencial 'digitalocean_spaces' não configurada em Integrações.");

        if (string.IsNullOrEmpty(cred.ClientIdCifrado) || string.IsNullOrEmpty(cred.ClientSecretCifrado))
            throw new InvalidOperationException(
                "Armazenamento Spaces: credencial incompleta (Access Key / Secret Key).");

        var accessKey = _protetor.Revelar(cred.ClientIdCifrado);
        var secretKey = _protetor.Revelar(cred.ClientSecretCifrado);
        var par = ParametrosSpaces.De(cred.ParametrosJson);

        if (string.IsNullOrWhiteSpace(par.Endpoint) || string.IsNullOrWhiteSpace(par.Bucket))
            throw new InvalidOperationException(
                "Armazenamento Spaces: informe 'endpoint' e 'bucket' no parametrosJson da credencial.");

        var config = new AmazonS3Config
        {
            ServiceURL = par.Endpoint,
            // Bucket com pontos (ex.: images.pacs.marica.automais.tec.br) exige path-style:
            // virtual-hosted quebraria a validação do certificado TLS.
            ForcePathStyle = par.ForcePathStyle ?? true,
            // DigitalOcean Spaces ainda não implementa os checksums (CRC) que o AWS SDK v4
            // passou a calcular por padrão — só calcula/valida quando a operação exige.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (!string.IsNullOrWhiteSpace(par.Region))
            config.AuthenticationRegion = par.Region;

        _cliente = new AmazonS3Client(accessKey, secretKey, config);
        _bucket = par.Bucket;
        return (_cliente, _bucket);
    }

    public void Dispose() => _cliente?.Dispose();

    /// <summary>Config pública do destino (não-secreta), em <c>parametrosJson</c> da credencial.</summary>
    private sealed record ParametrosSpaces(string? Endpoint, string? Region, string? Bucket, bool? ForcePathStyle)
    {
        public static ParametrosSpaces De(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new ParametrosSpaces(null, null, null, null);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var raiz = doc.RootElement;
                string? Texto(string nome) =>
                    raiz.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                bool? Booleano(string nome) =>
                    raiz.TryGetProperty(nome, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? v.GetBoolean()
                        : null;
                return new ParametrosSpaces(
                    Texto("endpoint"), Texto("region"), Texto("bucket"), Booleano("forcePathStyle"));
            }
            catch (JsonException)
            {
                return new ParametrosSpaces(null, null, null, null);
            }
        }
    }
}
