using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Worklist;

public sealed class Dcm4cheeUpsClient : IDcm4cheeUpsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<Dcm4cheeUpsClient> _logger;
    private readonly Dcm4cheeUpsOptions _options;

    public Dcm4cheeUpsClient(
        HttpClient http,
        ILogger<Dcm4cheeUpsClient> logger,
        IOptions<Dcm4cheeUpsOptions> options)
    {
        _http = http;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> CriarWorkitemAsync(SolicitacaoExame solicitacao, CancellationToken cancellationToken = default)
    {
        var workitemUid = string.IsNullOrWhiteSpace(solicitacao.WorklistItemUid)
            ? GerarUid()
            : solicitacao.WorklistItemUid;

        var payload = ConstrutorWorkitemUps.Construir(solicitacao, _options.StationAeTitle);

        var requisicao = new HttpRequestMessage(HttpMethod.Post, $"workitems?workitem={workitemUid}")
        {
            Content = JsonContent.Create<JsonArray>(payload, new MediaTypeHeaderValue("application/dicom+json")),
        };
        requisicao.Headers.Accept.Clear();
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(requisicao, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha de rede ao criar workitem no dcm4chee.");
            throw new ConflitoException("pacs.indisponivel", "Não foi possível alcançar o dcm4chee.");
        }

        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "dcm4chee rejeitou workitem ({Status}): {Corpo}",
                (int)resposta.StatusCode, corpo);
            throw new ConflitoException(
                "ups.criacao_falhou",
                $"dcm4chee respondeu {(int)resposta.StatusCode}: {Encurtar(corpo)}");
        }

        // Sucesso: dcm4chee responde 201 Created. O UID que enviamos no query é o efetivo.
        return workitemUid;
    }

    public async Task CancelarWorkitemAsync(string workitemUid, string motivo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workitemUid)) return;

        // PUT /workitems/{uid}/state com payload [{ "00741000": { "vr":"CS", "Value":["CANCELED"] } }]
        var payload = new JsonArray
        {
            new JsonObject
            {
                ["00741000"] = new JsonObject
                {
                    ["vr"] = "CS",
                    ["Value"] = new JsonArray("CANCELED"),
                },
            },
        };

        var requisicao = new HttpRequestMessage(HttpMethod.Put, $"workitems/{workitemUid}/state")
        {
            Content = JsonContent.Create<JsonArray>(payload, new MediaTypeHeaderValue("application/dicom+json")),
        };

        try
        {
            var resposta = await _http.SendAsync(requisicao, cancellationToken);
            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Cancelamento de workitem {Uid} retornou {Status}.", workitemUid, (int)resposta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: não derruba o cancelamento local se o dcm4chee estiver fora.
            _logger.LogWarning(ex, "Falha ao cancelar workitem {Uid} no dcm4chee — segue cancelado localmente.", workitemUid);
        }
    }

    private static string GerarUid()
    {
        // Prefixo "2.25." + Guid numerico (DICOM PS3.5 B.2)
        var bigint = new System.Numerics.BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true);
        return "2.25." + bigint.ToString();
    }

    private static string Encurtar(string s) => s.Length > 250 ? s[..250] + "…" : s;
}
