using Hl7.Fhir.Model;
using SMSMarica.Core.Atendimentos.Dtos;
using SMSMarica.Core.Atendimentos.Fhir;

namespace SMSMarica.Core.Atendimentos;

/// <summary>
/// Lê o histórico clínico do paciente no hub FHIR (Encounter + Condition) e o
/// projeta para o smsmarica. O id do paciente no smsmarica É o id do Patient no
/// hub (paciente é proxy do hub).
/// </summary>
public sealed class AtendimentosService(IEncounterFhirClient fhir) : IAtendimentosService
{
    public async Task<IReadOnlyList<AtendimentoDto>> ObterPorPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var encBundle = await fhir.BuscarEncountersAsync(pacienteId, cancellationToken);
        var condBundle = await fhir.BuscarConditionsAsync(pacienteId, cancellationToken);
        var docBundle = await fhir.BuscarDocumentsAsync(pacienteId, cancellationToken);

        // Agrupa diagnósticos por id do Encounter referenciado.
        var porEncounter = condBundle.Entry
            .Select(e => e.Resource)
            .OfType<Condition>()
            .Where(c => c.Encounter?.Reference is not null)
            .GroupBy(c => IdDaReferencia(c.Encounter!.Reference!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Agrupa documentos por id do Encounter referenciado (context.encounter).
        var docsPorEncounter = docBundle.Entry
            .Select(e => e.Resource)
            .OfType<DocumentReference>()
            .Where(d => d.Context?.Encounter?.FirstOrDefault()?.Reference is not null)
            .GroupBy(d => IdDaReferencia(d.Context!.Encounter!.First().Reference!))
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

            var documentos = docsPorEncounter.TryGetValue(enc.Id!, out var docs)
                ? docs.Select(d => new DocumentoDto(
                    Guid.Parse(d.Id!),
                    d.Type?.Text ?? "Documento",
                    d.Date,
                    DecodificarHtml(d.Content?.FirstOrDefault()?.Attachment))).ToList()
                : [];

            atendimentos.Add(new AtendimentoDto(
                id,
                ParseData(enc.Period?.Start),
                ParseData(enc.Period?.End),
                RotuloClasse(enc.Class?.Code),
                enc.Status?.ToString().ToLowerInvariant() ?? "unknown",
                enc.Participant?.FirstOrDefault()?.Individual?.Display,
                enc.Meta?.Source,
                diagnosticos,
                documentos));
        }

        // Mais recente primeiro (o hub já ordena, mas garante).
        return [.. atendimentos.OrderByDescending(a => a.Inicio)];
    }

    private static string RotuloClasse(string? code) => code switch
    {
        "EMER" => "Urgência",
        "AMB" => "Ambulatorial",
        "IMP" => "Internação",
        _ => "Atendimento",
    };

    private static string IdDaReferencia(string reference) => reference.Split('/')[^1];

    private static string DecodificarHtml(Attachment? a) =>
        a?.Data is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : string.Empty;

    private static DateTimeOffset? ParseData(string? d) =>
        DateTimeOffset.TryParse(d, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var r) ? r : null;
}
