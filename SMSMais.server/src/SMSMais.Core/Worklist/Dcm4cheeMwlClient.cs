using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Worklist;

public sealed class Dcm4cheeMwlClient : IDcm4cheeMwlClient
{
    private readonly HttpClient _http;
    private readonly ILogger<Dcm4cheeMwlClient> _logger;
    private readonly IPacienteResolver _pacienteResolver;
    private readonly IResolvedorEstacaoWorklist _estacao;

    private static readonly MediaTypeHeaderValue DicomJson = new("application/dicom+json");

    public Dcm4cheeMwlClient(
        HttpClient http,
        ILogger<Dcm4cheeMwlClient> logger,
        IPacienteResolver pacienteResolver,
        IResolvedorEstacaoWorklist estacao)
    {
        _http = http;
        _logger = logger;
        _pacienteResolver = pacienteResolver;
        _estacao = estacao;
    }

    public async Task<string> CriarOuAtualizarMwlItemAsync(ExameImagem s, CancellationToken ct = default)
    {
        var paciente = await ResolverPacienteAsync(s.Solicitacao!.PacienteId, ct);

        // 1) Garante o paciente no dcm4chee (o POST /mwlitems exige paciente existente).
        await RegistrarPacienteAsync(s, paciente, ct);

        // 2) Cria/atualiza o MWL item (upsert por StudyInstanceUID + SPS ID). O AE da
        //    estação vem do equipamento da unidade executante (fallback no appsettings).
        var estacao = await _estacao.ResolverAsync(s, ct);
        var item = ConstrutorMwlItem.Item(
            s, paciente, estacao.AeTitle, estacao.DescricaoMaxCaracteres);
        var req = new HttpRequestMessage(HttpMethod.Post, "mwlitems")
        {
            Content = JsonContent.Create(item, DicomJson),
        };
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var resp = await EnviarAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("dcm4chee recusou MWL item de {Accession} ({Status}): {Corpo}",
                s.AccessionNumber, (int)resp.StatusCode, Encurtar(corpo));
            throw new ConflitoException("mwl.criacao_falhou",
                $"dcm4chee respondeu {(int)resp.StatusCode} ao criar o item de worklist.");
        }

        return ConstrutorMwlItem.SpsId(s);
    }

    public async Task<bool> MwlItemExisteAsync(ExameImagem s, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"mwlitems?StudyInstanceUID={Uri.EscapeDataString(s.StudyInstanceUID)}&includefield=00080050");
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dicom+json"));

        var resp = await EnviarAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.OK)
        {
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            var arr = string.IsNullOrWhiteSpace(corpo) ? null : JsonNode.Parse(corpo) as JsonArray;
            return arr is { Count: > 0 };
        }
        if (resp.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
        {
            return false;
        }

        var txt = await resp.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("GET mwlitems de {Accession} retornou {Status}: {Corpo}",
            s.AccessionNumber, (int)resp.StatusCode, Encurtar(txt));
        throw new ConflitoException("pacs.indisponivel",
            $"dcm4chee respondeu {(int)resp.StatusCode} ao consultar a worklist.");
    }

    public async Task<bool> ExcluirMwlItemAsync(ExameImagem s, CancellationToken ct = default)
    {
        var sps = ConstrutorMwlItem.SpsId(s);
        var req = new HttpRequestMessage(
            HttpMethod.Delete,
            $"mwlitems/{Uri.EscapeDataString(s.StudyInstanceUID)}/{Uri.EscapeDataString(sps)}");

        var resp = await EnviarAsync(req, ct);

        // 2xx = removido; 404 = já não existia (também satisfaz "garantir que sumiu").
        if (resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        var corpo = await resp.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("dcm4chee recusou DELETE do MWL item {Sps} ({Status}): {Corpo}",
            sps, (int)resp.StatusCode, Encurtar(corpo));
        throw new ConflitoException("mwl.exclusao_falhou",
            $"dcm4chee respondeu {(int)resp.StatusCode} ao excluir o item de worklist.");
    }

    // ---- helpers ----

    private async Task<PacienteResumo> ResolverPacienteAsync(Guid pacienteId, CancellationToken ct)
    {
        try
        {
            var p = await _pacienteResolver.ResolverAsync(pacienteId, ct);
            if (p is not null) return p;
            _logger.LogWarning("Paciente {Id} não encontrado no hub FHIR — MWL com placeholder.", pacienteId);
            return new PacienteResumo(pacienteId, "PACIENTE", null, null, null, Sexo.NaoInformado);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao resolver paciente {Id} no hub FHIR.", pacienteId);
            throw new ConflitoException("fhir.indisponivel", "Não foi possível resolver o paciente no hub FHIR.");
        }
    }

    private async Task RegistrarPacienteAsync(ExameImagem s, PacienteResumo paciente, CancellationToken ct)
    {
        var corpo = ConstrutorMwlItem.Paciente(s, paciente);
        var req = new HttpRequestMessage(HttpMethod.Post, "patients")
        {
            Content = JsonContent.Create(corpo, DicomJson),
        };
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var resp = await EnviarAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var txt = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("dcm4chee recusou registro do paciente {Id} ({Status}): {Corpo}",
                s.Solicitacao!.PacienteId, (int)resp.StatusCode, Encurtar(txt));
            throw new ConflitoException("pacs.paciente_falhou",
                $"dcm4chee respondeu {(int)resp.StatusCode} ao registrar o paciente.");
        }
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(req, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha de rede ao falar com o dcm4chee ({Metodo} {Uri}).", req.Method, req.RequestUri);
            throw new ConflitoException("pacs.indisponivel", "Não foi possível alcançar o dcm4chee.");
        }
    }

    private static string Encurtar(string s) => s.Length > 250 ? s[..250] + "…" : s;
}
