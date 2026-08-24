using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Laudos.Assinatura;

/// <summary>
/// Implementação de <see cref="IAssinadorPdfPades"/> que delega ao serviço aberto
/// <c>Automais.Assinador</c> (iText) por HTTP. Mantém o PDF dentro da nossa rede e
/// isola a lib AGPL atrás de uma fronteira de processo.
/// </summary>
public sealed class AssinadorPdfHttpClient(
    HttpClient http,
    ILogger<AssinadorPdfHttpClient> logger) : IAssinadorPdfPades
{
    public async Task<PreparacaoAssinatura> PrepararAsync(
        byte[] pdfOriginal,
        IReadOnlyList<byte[]> cadeiaCertificado,
        DadosVisualAssinatura visual,
        CancellationToken cancellationToken = default)
    {
        var req = new PrepararReq(
            Convert.ToBase64String(pdfOriginal),
            [.. cadeiaCertificado.Select(Convert.ToBase64String)],
            visual.NomeMedico, visual.Crm, visual.UfCrm, visual.Rqe, visual.TextoRodape,
            visual.CarimboPngBase64);

        var resp = await EnviarAsync<PrepararReq, PrepararResp>("pades/preparar", req, cancellationToken);

        return new PreparacaoAssinatura(
            Convert.FromBase64String(resp.ToSignHashBase64),
            resp.AlgoritmoHash,
            Convert.FromBase64String(resp.TransferStateBase64));
    }

    public async Task<ResultadoAssinatura> ConcluirAsync(
        byte[] transferState,
        byte[] assinaturaCliente,
        CancellationToken cancellationToken = default)
    {
        var req = new ConcluirReq(
            Convert.ToBase64String(transferState),
            Convert.ToBase64String(assinaturaCliente));

        var resp = await EnviarAsync<ConcluirReq, ConcluirResp>("pades/concluir", req, cancellationToken);

        return new ResultadoAssinatura(
            Convert.FromBase64String(resp.PdfAssinadoBase64),
            resp.Formato,
            resp.CertificadoTitular,
            resp.CertificadoEmissor,
            resp.CpfTitular);
    }

    private async Task<TResp> EnviarAsync<TReq, TResp>(string caminho, TReq corpo, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsJsonAsync(caminho, corpo, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Falha de rede ao chamar o Automais.Assinador ({Caminho}).", caminho);
            throw new ConflitoException("assinador.indisponivel", "Não foi possível alcançar o serviço de assinatura.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout ao chamar o Automais.Assinador ({Caminho}).", caminho);
            throw new ConflitoException("assinador.timeout", "O serviço de assinatura excedeu o tempo limite.");
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var detalhe = await resp.Content.ReadAsStringAsync(ct);
                logger.LogError("Automais.Assinador retornou {Status} em {Caminho}: {Detalhe}",
                    (int)resp.StatusCode, caminho, detalhe);
                throw new ConflitoException("assinador.erro",
                    $"O serviço de assinatura falhou ({(int)resp.StatusCode}).");
            }

            return await resp.Content.ReadFromJsonAsync<TResp>(ct)
                ?? throw new ConflitoException("assinador.resposta_vazia", "Resposta vazia do serviço de assinatura.");
        }
    }

    private sealed record PrepararReq(
        string PdfBase64,
        IReadOnlyList<string> CadeiaCertificadoBase64,
        string NomeMedico,
        string Crm,
        string UfCrm,
        string? Rqe,
        string TextoRodape,
        string? CarimboPngBase64);

    private sealed record PrepararResp(string ToSignHashBase64, string AlgoritmoHash, string TransferStateBase64);

    private sealed record ConcluirReq(string TransferStateBase64, string RawSignatureBase64);

    private sealed record ConcluirResp(
        string PdfAssinadoBase64,
        string Formato,
        string? CertificadoTitular,
        string? CertificadoEmissor,
        string? CpfTitular);
}
