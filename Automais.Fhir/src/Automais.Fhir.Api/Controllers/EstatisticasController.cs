using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Data;

namespace Automais.Fhir.Api.Controllers;

/// <summary>
/// Contagens por tipo de recurso, opcionalmente filtradas por <c>meta.source</c> — insumo do
/// diagnóstico origem×hub do sincronismo contínuo (ADR-0024). Consulta só as colunas de
/// metadados (nunca abre o jsonb). Não é um endpoint FHIR — devolve JSON simples.
/// </summary>
[ApiController]
[Route("fhir/_estatisticas")]
public sealed class EstatisticasController(FhirDbContext db) : ControllerBase
{
    /// <summary>GET /fhir/_estatisticas?source=https://.../salux/salux-hcml</summary>
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] string? source, CancellationToken ct)
    {
        async Task<int> Contar<T>(IQueryable<T> set) where T : Data.Entities.ResourceRow
        {
            var q = set.AsNoTracking().Where(r => !r.IsDeleted);
            if (!string.IsNullOrWhiteSpace(source)) q = q.Where(r => r.MetaSource == source);
            return await q.CountAsync(ct);
        }

        var encounters = db.Encounters.AsNoTracking().Where(e => !e.IsDeleted);
        if (!string.IsNullOrWhiteSpace(source)) encounters = encounters.Where(e => e.MetaSource == source);

        return Ok(new
        {
            source,
            patients = await Contar(db.Patients),
            practitioners = await Contar(db.Practitioners),
            encounters = await Contar(db.Encounters),
            encountersInternacao = await encounters.CountAsync(e => e.Classe == "IMP", ct),
            encountersInternacaoEmCurso = await encounters.CountAsync(e => e.Classe == "IMP" && e.Status == "in-progress", ct),
            conditions = await Contar(db.Conditions),
            documentReferences = await Contar(db.DocumentReferences),
            medicationRequests = await Contar(db.MedicationRequests),
            observations = await Contar(db.Observations),
            locations = await Contar(db.Locations),
        });
    }
}
