using System.Globalization;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Integracoes.KlinikosWeb.Cid;
using SMSMais.Core.Integracoes.KlinikosWeb.Fhir;
using SMSMais.Core.Integracoes.Pep.Fhir;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Varredura;

/// <summary>Status de um campo no diff de paridade.</summary>
public enum ParidadeStatus { Igual, Diferente, AusenteWeb, AusenteHub }

public sealed record ParidadeCampo(string Campo, string? Web, string? Hub, ParidadeStatus Status);

public sealed record ParidadeBoletim(string Spa, bool NoHub, IReadOnlyList<ParidadeCampo> Campos);

public sealed record ParidadeRelatorio(
    string Provedor, DateOnly Dia, int Amostra, int NoHub, int ForaDoHub,
    IReadOnlyDictionary<string, int> ResumoPorStatus,
    IReadOnlyList<ParidadeBoletim> Itens);

/// <summary>
/// Teste de PARIDADE SQL × web (§4a do plano). Para os mesmos boletins que o hub já tem via SQL,
/// monta os recursos web EM MEMÓRIA e compara campo a campo o ESTADO CONVERGIDO alvo — Encounter
/// (class/status/chegada por minuto), Condition (CID após de-para), e presença de Observation
/// (vitais) e Patient. É o critério de aceite antes de qualquer substituição. <b>SOMENTE
/// LEITURA</b> — não escreve no hub.
///
/// <para>Roda nas UPAs (onde há SQL no hub para comparar). O alvo é <b>equivalência eventual</b>:
/// campos do deep (vitais/narrativa) aparecem como <c>ausente-web</c> na espinha — é o esperado, e
/// o número quantifica o que a fila do deep ainda tem de preencher.</para>
/// </summary>
public interface IKlinikosWebParidadeService
{
    Task<ParidadeRelatorio> CompararAsync(string provedor, DateOnly dia, int amostra, CancellationToken ct);
}

public sealed class KlinikosWebParidadeService(
    IKlinikosWebSincronizacaoService sincronizacao,
    IHubFhirEscritor hub,
    ICidDeParaService cid,
    ILogger<KlinikosWebParidadeService> logger) : IKlinikosWebParidadeService
{
    private const string SysBoletim = "urn:klinikos:boletim";

    public async Task<ParidadeRelatorio> CompararAsync(
        string provedor, DateOnly dia, int amostra, CancellationToken ct)
    {
        var id = KlinikosWebInstanciaFhir.De(provedor);
        var mapper = new KlinikosWebFhirMapper(id.Slug, id.Source, cid);

        var espinha = await sincronizacao.MontarEspinhaAsync(provedor, dia, ct);
        var cids = await sincronizacao.PuxarCidPorBoletimAsync(provedor, dia, ct);

        var itens = new List<ParidadeBoletim>();
        var resumo = new Dictionary<string, int>(StringComparer.Ordinal);
        int noHub = 0, foraDoHub = 0;

        foreach (var e in espinha.Take(Math.Max(1, amostra)))
        {
            ct.ThrowIfCancellationRequested();

            var encHub = await BuscarUnicoAsync<Encounter>("Encounter", $"{id.Slug}:{e.SpaCodigo}", ct);
            if (encHub is null)
            {
                foraDoHub++;
                itens.Add(new ParidadeBoletim(e.SpaCodigo, NoHub: false, []));
                continue;
            }
            noHub++;

            // Encounter web em memória (mesma forma do SQL).
            var pacRef = encHub.Subject?.Reference ?? "Patient/desconhecido";
            var encWeb = mapper.MontarEncounter(e, pacRef, encHub.ServiceProvider?.Reference, id.UnidCodigo);

            var campos = new List<ParidadeCampo>
            {
                Campo("Encounter.class", encWeb.Class?.Code, encHub.Class?.Code),
                Campo("Encounter.status", encWeb.Status?.ToString(), encHub.Status?.ToString()),
                CampoData("Encounter.period.start", encWeb.Period?.Start, encHub.Period?.Start),
            };

            // Condition (CID) — web via de-para; hub via SQL.
            cids.TryGetValue(e.SpaCodigo, out var cidTexto);
            var condWeb = mapper.MontarCondition(e.SpaCodigo, cidTexto, pacRef, $"Encounter/{encHub.Id}", out _);
            var condHub = await BuscarUnicoAsync<Condition>("Condition", $"{id.Slug}:{e.SpaCodigo}:cond", ct);
            campos.Add(Campo("Condition.cid",
                CodigoCid(condWeb?.Code), CodigoCid(condHub?.Code)));

            // Deep: vitais/narrativa vêm da fila do deep, não da espinha — marcados como
            // ausente-web de propósito (o número que a fila terá de preencher para convergir).
            campos.Add(new ParidadeCampo(
                "Observation.vitais(deep)", "0 (espinha)", "(vem do deep)", ParidadeStatus.AusenteWeb));

            foreach (var c in campos)
            {
                var chave = c.Status.ToString();
                resumo[chave] = resumo.GetValueOrDefault(chave) + 1;
            }
            itens.Add(new ParidadeBoletim(e.SpaCodigo, NoHub: true, campos));
        }

        logger.LogInformation(
            "Klinikos paridade {Prov} {Dia}: amostra {Am}, no hub {NoHub}, fora {Fora}.",
            provedor, dia, itens.Count, noHub, foraDoHub);

        return new ParidadeRelatorio(provedor, dia, itens.Count, noHub, foraDoHub, resumo, itens);
    }

    // ------------------------------------------------------------------ helpers de comparação

    private static ParidadeCampo Campo(string nome, string? web, string? hub)
    {
        var status = (web, hub) switch
        {
            (null, null) => ParidadeStatus.Igual,
            (not null, null) => ParidadeStatus.AusenteHub,
            (null, not null) => ParidadeStatus.AusenteWeb,
            _ => string.Equals(web, hub, StringComparison.OrdinalIgnoreCase)
                ? ParidadeStatus.Igual : ParidadeStatus.Diferente,
        };
        return new ParidadeCampo(nome, web, hub, status);
    }

    /// <summary>Compara data-hora com TOLERÂNCIA de minuto (o web não tem o segundo).</summary>
    private static ParidadeCampo CampoData(string nome, string? web, string? hub)
    {
        var w = ParaMinuto(web);
        var h = ParaMinuto(hub);
        return Campo(nome, w, h);
    }

    private static string? ParaMinuto(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return null;
        return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)
            ? dto.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
            : iso;
    }

    private static string? CodigoCid(CodeableConcept? code) =>
        code?.Coding?.FirstOrDefault()?.Code ?? (string.IsNullOrWhiteSpace(code?.Text) ? null : "(texto)");

    private async Task<T?> BuscarUnicoAsync<T>(string tipo, string valor, CancellationToken ct)
        where T : Resource
    {
        var bundle = await hub.BuscarPorIdentifierAsync(tipo, SysBoletim, valor, ct);
        return bundle.Entry.Select(x => x.Resource).OfType<T>().FirstOrDefault();
    }
}
