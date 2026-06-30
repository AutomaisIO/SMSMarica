using System.Text;
using System.Text.RegularExpressions;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// Implementação da transcodificação JPEG-LS Lossless. Busca a instância DICOM
/// COMPLETA do PACS via WADO-URI (<c>contentType=application/dicom</c> — devolve
/// o arquivo cru, sem multipart, fácil de abrir com fo-dicom), transcoda o
/// dataset para o transfer-syntax alvo (default <c>1.2.840.10008.1.2.4.80</c>),
/// extrai o frame e embrulha no envelope <c>multipart/related</c> que o loader do
/// Cornerstone espera. Lossless = pixel-idêntico; metadados (rows/cols/bits/...)
/// não mudam, então continuam válidos.
/// </summary>
public sealed partial class PacsTranscodeService : IPacsTranscodeService
{
    // Sufixo que discrimina a chave de cache da variante comprimida da chave do
    // frame cru (mesma URL /instances/.../frames/n). Não vai pela rede — só entra
    // no cálculo da chave de cache.
    private const string SufixoComprimido = "#jls";

    private readonly HttpClient _http;
    private readonly ILogger<PacsTranscodeService> _logger;
    private readonly bool _habilitado;
    private readonly string _transferSyntaxAlvo;
    private readonly string? _wadoUriBase;

    // Extrai study/series/sop/frame de um caminho WADO-RS de frame.
    [GeneratedRegex(@"studies/(?<study>[^/]+)/series/(?<series>[^/]+)/instances/(?<sop>[^/]+)/frames/(?<frame>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexFrame();

    public PacsTranscodeService(HttpClient http, IConfiguration configuration, ILogger<PacsTranscodeService> logger)
    {
        _http = http;
        _logger = logger;
        _habilitado = configuration.GetValue("Pacs:Compressao:Habilitado", false);
        _transferSyntaxAlvo = configuration["Pacs:Compressao:TransferSyntaxAlvo"]
            ?? "1.2.840.10008.1.2.4.80"; // JPEG-LS Lossless
        _wadoUriBase = DerivarWadoUri(configuration["Pacs:Dcm4chee:RsBaseUrl"]);
    }

    public bool Habilitado => _habilitado;

    public string DiscriminarCaminho(string caminho) => caminho + SufixoComprimido;

    public async Task<PacsFrameComprimido?> TranscodificarFrameAsync(string caminho, CancellationToken cancellationToken = default)
    {
        if (!_habilitado || string.IsNullOrWhiteSpace(_wadoUriBase)) return null;

        var match = RegexFrame().Match(caminho ?? string.Empty);
        if (!match.Success) return null;

        var study = match.Groups["study"].Value;
        var series = match.Groups["series"].Value;
        var sop = match.Groups["sop"].Value;
        // WADO-RS frame é 1-based; fo-dicom GetFrame é 0-based.
        var frameIndex = int.TryParse(match.Groups["frame"].Value, out var n) && n > 0 ? n - 1 : 0;

        try
        {
            PacsDicomSetup.Inicializar(); // garante codecs nativos registrados (idempotente)

            // 1) Busca a instância COMPLETA (application/dicom) via WADO-URI.
            var url = $"{_wadoUriBase}?requestType=WADO&studyUID={study}&seriesUID={series}" +
                      $"&objectUID={sop}&contentType=application/dicom";

            using var resposta = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning("WADO-URI retornou {Status} para a instância {Sop}.", (int)resposta.StatusCode, sop);
                return null;
            }

            await using var origem = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            var arquivo = await DicomFile.OpenAsync(origem);

            // 2) Transcoda o dataset para o alvo (JPEG-LS Lossless) e extrai o frame.
            var sintaxeOrigem = arquivo.Dataset.InternalTransferSyntax;
            var sintaxeAlvo = DicomTransferSyntax.Parse(_transferSyntaxAlvo);

            // Já está no alvo? Reusa direto (evita roundtrip de codec).
            var dataset = sintaxeOrigem == sintaxeAlvo
                ? arquivo.Dataset
                : new DicomTranscoder(sintaxeOrigem, sintaxeAlvo).Transcode(arquivo.Dataset);

            var pixelData = DicomPixelData.Create(dataset);
            if (frameIndex >= pixelData.NumberOfFrames) frameIndex = 0;
            var frameBytes = pixelData.GetFrame(frameIndex).Data;

            // 3) Embrulha no envelope multipart exigido pelo loader do Cornerstone.
            return MontarEnvelope(frameBytes);
        }
        catch (Exception ex)
        {
            // NUNCA propaga: o chamador serve o frame cru (fallback seguro).
            _logger.LogWarning(ex, "Falha ao transcodificar o frame {Caminho} para JPEG-LS.", caminho);
            return null;
        }
    }

    /// <summary>
    /// Monta o envelope <c>multipart/related</c> com UMA parte, byte-a-byte, com
    /// framing CRLF exato exigido pelo parser do Cornerstone (extractMultipart):
    /// o transfer-syntax PRECISA estar no Content-Type DA PARTE, e os bytes do
    /// frame DEVEM ser seguidos por <c>\r\n--BOUNDARY--</c>.
    /// </summary>
    private PacsFrameComprimido MontarEnvelope(byte[] frameBytes)
    {
        var boundary = Guid.NewGuid().ToString("N");
        const string crlf = "\r\n";

        var cabecalhoParte =
            $"--{boundary}{crlf}" +
            $"Content-Type: image/jls; transfer-syntax={_transferSyntaxAlvo}{crlf}" +
            crlf;
        var rodape = $"{crlf}--{boundary}--";

        var cabecalhoBytes = Encoding.ASCII.GetBytes(cabecalhoParte);
        var rodapeBytes = Encoding.ASCII.GetBytes(rodape);

        var corpo = new byte[cabecalhoBytes.Length + frameBytes.Length + rodapeBytes.Length];
        Buffer.BlockCopy(cabecalhoBytes, 0, corpo, 0, cabecalhoBytes.Length);
        Buffer.BlockCopy(frameBytes, 0, corpo, cabecalhoBytes.Length, frameBytes.Length);
        Buffer.BlockCopy(rodapeBytes, 0, corpo, cabecalhoBytes.Length + frameBytes.Length, rodapeBytes.Length);

        var contentType =
            $"multipart/related; type=\"image/jls\"; boundary={boundary}; transfer-syntax={_transferSyntaxAlvo}";

        return new PacsFrameComprimido(corpo, contentType);
    }

    /// <summary>
    /// Deriva a URL WADO-URI a partir da RsBaseUrl trocando o sufixo <c>/rs/</c>
    /// por <c>/wado</c> (mesmo AE/host/porta). Ex.: <c>.../aets/PACS-CDT/rs/</c>
    /// → <c>.../aets/PACS-CDT/wado</c>. Null se a base não tiver o sufixo esperado.
    /// </summary>
    private static string? DerivarWadoUri(string? rsBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(rsBaseUrl)) return null;
        var semBarra = rsBaseUrl.TrimEnd('/');
        if (semBarra.EndsWith("/rs", StringComparison.OrdinalIgnoreCase))
        {
            return semBarra[..^3] + "/wado"; // remove "/rs", adiciona "/wado"
        }
        return null;
    }
}
