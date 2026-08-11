using Hl7.Fhir.Model;
using Microsoft.Extensions.Caching.Memory;
using SMSMarica.Core.Atendimentos.Dtos;
using SMSMarica.Core.Atendimentos.Fhir;

namespace SMSMarica.Core.Atendimentos;

/// <summary>
/// Lê o histórico clínico do paciente no hub FHIR (Encounter + Condition) e o
/// projeta para o smsmarica. O id do paciente no smsmarica É o id do Patient no
/// hub (paciente é proxy do hub).
/// </summary>
public sealed class AtendimentosService(IEncounterFhirClient fhir, IMemoryCache cache) : IAtendimentosService
{
    private const string ChaveCacheOrganizacoes = "atendimentos:organizacoes";
    private static readonly TimeSpan ValidadeOrganizacoes = TimeSpan.FromMinutes(10);
    private const string SistemaCnes = "https://fhir.saude.gov.br/sid/cnes";

    public async Task<IReadOnlyList<AtendimentoDto>> ObterPorPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var unidades = await UnidadesAsync(cancellationToken);
        var encBundle = await fhir.BuscarEncountersAsync(pacienteId, cancellationToken);
        var condBundle = await fhir.BuscarConditionsAsync(pacienteId, cancellationToken);
        var medBundle = await fhir.BuscarMedicationRequestsAsync(pacienteId, cancellationToken);
        var docBundle = await fhir.BuscarDocumentsAsync(pacienteId, cancellationToken);
        var obsBundle = await fhir.BuscarObservationsAsync(pacienteId, cancellationToken);

        // Agrupa diagnósticos por id do Encounter referenciado.
        var porEncounter = condBundle.Entry
            .Select(e => e.Resource)
            .OfType<Condition>()
            .Where(c => c.Encounter?.Reference is not null)
            .GroupBy(c => IdDaReferencia(c.Encounter!.Reference!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Agrupa medicamentos (MedicationRequest) por id do Encounter referenciado.
        var medsPorEncounter = medBundle.Entry
            .Select(e => e.Resource)
            .OfType<MedicationRequest>()
            .Where(m => m.Encounter?.Reference is not null)
            .GroupBy(m => IdDaReferencia(m.Encounter!.Reference!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Agrupa documentos por id do Encounter referenciado (context.encounter).
        var docsPorEncounter = docBundle.Entry
            .Select(e => e.Resource)
            .OfType<DocumentReference>()
            .Where(d => d.Context?.Encounter?.FirstOrDefault()?.Reference is not null)
            .GroupBy(d => IdDaReferencia(d.Context!.Encounter!.First().Reference!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Agrupa observações (sinais vitais + risco) por id do Encounter referenciado.
        var obsPorEncounter = obsBundle.Entry
            .Select(e => e.Resource)
            .OfType<Observation>()
            .Where(o => o.Encounter?.Reference is not null)
            .GroupBy(o => IdDaReferencia(o.Encounter!.Reference!))
            .ToDictionary(g => g.Key, g => g.ToList());

        var atendimentos = new List<AtendimentoDto>();
        foreach (var enc in encBundle.Entry.Select(e => e.Resource).OfType<Encounter>())
        {
            var id = Guid.Parse(enc.Id!);
            var diagnosticos = porEncounter.TryGetValue(enc.Id!, out var conds)
                ? conds.Select(c => new DiagnosticoDto(
                    c.Code?.Coding?.FirstOrDefault()?.Code ?? string.Empty,
                    c.Code?.Text ?? c.Code?.Coding?.FirstOrDefault()?.Display)).ToList()
                : [];

            var medicamentos = medsPorEncounter.TryGetValue(enc.Id!, out var meds)
                ? meds.Select(m => new MedicamentoDto(
                    Guid.Parse(m.Id!),
                    (m.Medication as CodeableConcept)?.Text
                        ?? (m.Medication as CodeableConcept)?.Coding?.FirstOrDefault()?.Display
                        ?? "Medicamento",
                    m.DosageInstruction?.FirstOrDefault()?.Text,
                    m.Priority == RequestPriority.Urgent)).ToList()
                : [];

            var documentos = docsPorEncounter.TryGetValue(enc.Id!, out var docs)
                ? docs.Select(d => new DocumentoDto(
                    Guid.Parse(d.Id!),
                    d.Type?.Text ?? "Documento",
                    d.Date,
                    DecodificarHtml(d.Content?.FirstOrDefault()?.Attachment))).ToList()
                : [];

            var (sinaisVitais, risco) = obsPorEncounter.TryGetValue(enc.Id!, out var obs)
                ? ProjetarObservations(obs)
                : ([], null);

            var unidade = enc.ServiceProvider?.Reference is { } refUnidade
                && unidades.TryGetValue(IdDaReferencia(refUnidade), out var u) ? u : default;

            atendimentos.Add(new AtendimentoDto(
                id,
                ParseData(enc.Period?.Start),
                ParseData(enc.Period?.End),
                RotuloClasse(enc.Class?.Code),
                enc.Status?.ToString().ToLowerInvariant() ?? "unknown",
                enc.Participant?.FirstOrDefault()?.Individual?.Display,
                enc.Meta?.Source,
                unidade.Nome,
                unidade.Cnes,
                diagnosticos,
                medicamentos,
                documentos,
                sinaisVitais,
                risco));
        }

        // Mais recente primeiro (o hub já ordena, mas garante).
        return [.. atendimentos.OrderByDescending(a => a.Inicio)];
    }

    /// <summary>
    /// Catálogo de unidades (Organization) indexado pelo id, em cache curto. O hub tem 3, e o
    /// <c>serviceProvider</c> de todo Encounter aponta para uma delas — sem o catálogo o
    /// atendimento sabe de qual PEP veio mas não sabe onde aconteceu.
    /// </summary>
    private async Task<Dictionary<string, (string? Nome, string? Cnes)>> UnidadesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue<Dictionary<string, (string? Nome, string? Cnes)>>(ChaveCacheOrganizacoes, out var cacheado)
            && cacheado is not null)
        {
            return cacheado;
        }

        var bundle = await fhir.BuscarOrganizacoesAsync(ct);
        var mapa = bundle.Entry
            .Select(e => e.Resource)
            .OfType<Organization>()
            .Where(o => o.Id is not null)
            .ToDictionary(
                o => o.Id!,
                o => (o.Name, o.Identifier?.FirstOrDefault(i => i.System == SistemaCnes)?.Value));

        cache.Set(ChaveCacheOrganizacoes, mapa, ValidadeOrganizacoes);
        return mapa;
    }

    private static string RotuloClasse(string? code) => code switch
    {
        "EMER" => "Urgência",
        "AMB" => "Ambulatorial",
        "IMP" => "Internação",
        _ => "Atendimento",
    };

    // LOINC dos sinais vitais que projetamos (perfil vital-signs).
    private static readonly Dictionary<string, string> NomesVitais = new()
    {
        ["85354-9"] = "Pressão arterial",
        ["8867-4"] = "Freq. cardíaca",
        ["9279-1"] = "Freq. respiratória",
        ["8310-5"] = "Temperatura",
        ["2708-6"] = "Saturação O₂",
        ["29463-7"] = "Peso",
        ["8302-2"] = "Altura",
    };

    /// <summary>
    /// Separa as Observations de um atendimento em sinais vitais (perfil vital-signs, LOINC
    /// conhecido) e classificação de risco (Observation com valor CodeableConcept).
    /// </summary>
    private static (List<SinalVitalDto> Vitais, RiscoDto? Risco) ProjetarObservations(List<Observation> obs)
    {
        var vitais = new List<SinalVitalDto>();
        RiscoDto? risco = null;

        foreach (var o in obs)
        {
            var codigo = o.Code?.Coding?.FirstOrDefault()?.Code;
            var em = DataObservation(o);

            if (codigo == "85354-9")
            {
                // Pressão arterial: painel com componentes sistólica (8480-6) + diastólica (8462-4).
                var sist = ValorComponente(o, "8480-6");
                var diast = ValorComponente(o, "8462-4");
                vitais.Add(new SinalVitalDto("85354-9", "Pressão arterial", sist, diast, "mmHg", em));
            }
            else if (codigo is not null && NomesVitais.TryGetValue(codigo, out var nome))
            {
                var q = o.Value as Quantity;
                vitais.Add(new SinalVitalDto(codigo, nome, (double?)q?.Value, null, q?.Unit, em));
            }
            else if (o.Value is CodeableConcept cc)
            {
                // Classificação de risco (cor da triagem).
                var cor = cc.Coding?.FirstOrDefault()?.Display ?? cc.Text ?? "—";
                risco = new RiscoDto(cor, o.Code?.Text ?? cc.Text, em);
            }
        }

        return (vitais, risco);
    }

    private static double? ValorComponente(Observation o, string loinc) =>
        (double?)(o.Component
            .FirstOrDefault(c => c.Code?.Coding?.Any(cd => cd.Code == loinc) == true)?
            .Value as Quantity)?.Value;

    private static DateTimeOffset? DataObservation(Observation o) => o.Effective switch
    {
        FhirDateTime fdt => ParseData(fdt.Value),
        Period p => ParseData(p.Start),
        Instant inst => inst.Value,
        _ => null,
    };

    private static string IdDaReferencia(string reference) => reference.Split('/')[^1];

    private static string DecodificarHtml(Attachment? a) =>
        a?.Data is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : string.Empty;

    private static DateTimeOffset? ParseData(string? d) =>
        DateTimeOffset.TryParse(d, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var r) ? r : null;
}
