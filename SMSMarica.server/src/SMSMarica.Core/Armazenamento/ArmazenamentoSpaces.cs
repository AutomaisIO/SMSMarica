using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;

namespace SMSMarica.Core.Armazenamento;

/// <summary>Resultado do teste de conexão do Spaces (grava/lê/apaga um objeto pequeno).</summary>
public sealed record TesteArmazenamentoSpaces(bool Ok, string Etapa, string Mensagem, string? Bucket, string? Endpoint);

/// <summary>
/// Implementação DigitalOcean Spaces (S3) de <see cref="IArmazenamentoArquivos"/>.
/// Toda a configuração (Access/Secret Key, endpoint, region, bucket) vem do provedor
/// <c>digitalocean_spaces</c> em Integrações — cifrada em repouso (<see cref="IProtetorSegredos"/>),
/// <b>não</b> de variável de ambiente. Objetos privados; download só pelo backend.
/// Toda gravação é confirmada por leitura (read-back) para nunca deixar registro órfão.
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

    /// <summary>true se a credencial 'digitalocean_spaces' está completa e ativa (decide o provedor).</summary>
    public async Task<bool> DisponivelAsync(CancellationToken ct = default)
        => await LerConfigAsync(ct) is not null;

    public async Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
    {
        var (cliente, bucket) = await GarantirClienteAsync(cancellationToken);
        try
        {
            using (var stream = new MemoryStream(conteudo, writable: false))
            {
                await cliente.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = chave,
                    InputStream = stream,
                    ContentType = "application/pdf",
                }, cancellationToken);
            }

            // Confirmação (read-back): o objeto precisa existir com o tamanho gravado.
            // Se não confirmar, falha alto — o chamador NÃO cria o registro (sem órfão).
            var meta = await cliente.GetObjectMetadataAsync(bucket, chave, cancellationToken);
            if (meta.ContentLength != conteudo.Length)
            {
                throw new ArmazenamentoIndisponivelException(
                    "Falha ao confirmar o salvamento do arquivo (tamanho divergente) no armazenamento. Procure o suporte técnico.");
            }
        }
        catch (ArmazenamentoIndisponivelException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArmazenamentoIndisponivelException(
                "Falha ao salvar o arquivo no armazenamento (DigitalOcean Spaces). Procure o suporte técnico.", ex);
        }
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
        await cliente.DeleteObjectAsync(bucket, chave, cancellationToken); // idempotente no S3
    }

    /// <summary>
    /// Teste ponta-a-ponta da credencial: grava, lê e apaga um objeto pequeno.
    /// Nunca lança — sempre devolve o resultado (com a etapa que falhou, se houver).
    /// </summary>
    public async Task<TesteArmazenamentoSpaces> TestarAsync(CancellationToken ct = default)
    {
        ConfigSpaces? cfg;
        try
        {
            cfg = await LerConfigAsync(ct);
        }
        catch (Exception ex)
        {
            return new TesteArmazenamentoSpaces(false, "credencial", $"Erro ao ler a credencial: {ex.Message}", null, null);
        }
        if (cfg is null)
        {
            return new TesteArmazenamentoSpaces(false, "credencial",
                "Credencial do DigitalOcean Spaces ausente, inativa ou incompleta (Access/Secret Key + endpoint + bucket).",
                null, null);
        }

        IAmazonS3 cliente;
        try
        {
            cliente = CriarCliente(cfg);
        }
        catch (Exception ex)
        {
            return new TesteArmazenamentoSpaces(false, "conexao", $"Falha ao montar o cliente S3: {ex.Message}", cfg.Bucket, cfg.Endpoint);
        }

        var chave = $"{_prefixo}/_diagnostico/teste-{Guid.NewGuid():N}.txt";
        var conteudo = Encoding.UTF8.GetBytes("smsmarica-arquivos-teste");
        try
        {
            using (var stream = new MemoryStream(conteudo, writable: false))
            {
                await cliente.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = cfg.Bucket,
                    Key = chave,
                    InputStream = stream,
                    ContentType = "text/plain",
                }, ct);
            }

            using (var resp = await cliente.GetObjectAsync(cfg.Bucket, chave, ct))
            {
                using var ms = new MemoryStream();
                await resp.ResponseStream.CopyToAsync(ms, ct);
                if (ms.Length != conteudo.Length)
                {
                    await TentarApagar(cliente, cfg.Bucket, chave, ct);
                    return new TesteArmazenamentoSpaces(false, "leitura",
                        "Objeto de teste lido com tamanho divergente.", cfg.Bucket, cfg.Endpoint);
                }
            }

            await cliente.DeleteObjectAsync(cfg.Bucket, chave, ct);

            return new TesteArmazenamentoSpaces(true, "ok",
                $"Conexão OK — gravou, leu e apagou um objeto de teste no bucket {cfg.Bucket} (prefixo '{_prefixo}').",
                cfg.Bucket, cfg.Endpoint);
        }
        catch (Exception ex)
        {
            await TentarApagar(cliente, cfg.Bucket, chave, ct);
            return new TesteArmazenamentoSpaces(false, "escrita/leitura",
                $"Falha no teste de leitura/escrita: {ex.Message}", cfg.Bucket, cfg.Endpoint);
        }
        finally
        {
            cliente.Dispose();
        }
    }

    private static async Task TentarApagar(IAmazonS3 cliente, string bucket, string chave, CancellationToken ct)
    {
        try { await cliente.DeleteObjectAsync(bucket, chave, ct); }
        catch { /* limpeza best-effort */ }
    }

    /// <summary>Constrói o cliente S3 sob demanda (1x por scope) a partir da credencial cifrada.</summary>
    private async Task<(IAmazonS3 Cliente, string Bucket)> GarantirClienteAsync(CancellationToken ct)
    {
        if (_cliente is not null && _bucket is not null) return (_cliente, _bucket);

        var cfg = await LerConfigAsync(ct)
            ?? throw new ArmazenamentoIndisponivelException(
                "Armazenamento de arquivos indisponível: o DigitalOcean Spaces não está configurado/ativo em Integrações. Procure o suporte técnico.");

        _cliente = CriarCliente(cfg);
        _bucket = cfg.Bucket;
        return (_cliente, _bucket);
    }

    /// <summary>Lê e decifra a credencial. Retorna null se ausente / inativa / incompleta.</summary>
    private async Task<ConfigSpaces?> LerConfigAsync(CancellationToken ct)
    {
        var cred = await _db.IntegracaoCredenciais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Provedor == ProvedorSpaces, ct);
        if (cred is null || !cred.Ativo) return null;
        if (string.IsNullOrEmpty(cred.ClientIdCifrado) || string.IsNullOrEmpty(cred.ClientSecretCifrado)) return null;

        var par = ParametrosSpaces.De(cred.ParametrosJson);
        if (string.IsNullOrWhiteSpace(par.Endpoint) || string.IsNullOrWhiteSpace(par.Bucket)) return null;

        return new ConfigSpaces(
            _protetor.Revelar(cred.ClientIdCifrado),
            _protetor.Revelar(cred.ClientSecretCifrado),
            par.Endpoint!,
            par.Region,
            par.Bucket!,
            par.ForcePathStyle ?? true);
    }

    private static IAmazonS3 CriarCliente(ConfigSpaces cfg)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = cfg.Endpoint,
            // Bucket com pontos (ex.: images.pacs.marica.automais.tec.br) exige path-style.
            ForcePathStyle = cfg.ForcePathStyle,
            // DigitalOcean Spaces não implementa os checksums (CRC) que o AWS SDK v4
            // passou a calcular por padrão — só quando a operação realmente exige.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (!string.IsNullOrWhiteSpace(cfg.Region))
            config.AuthenticationRegion = cfg.Region;
        return new AmazonS3Client(cfg.AccessKey, cfg.SecretKey, config);
    }

    public void Dispose() => _cliente?.Dispose();

    private sealed record ConfigSpaces(
        string AccessKey, string SecretKey, string Endpoint, string? Region, string Bucket, bool ForcePathStyle);

    /// <summary>Config pública (não-secreta) do destino, em <c>parametrosJson</c> da credencial.</summary>
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
