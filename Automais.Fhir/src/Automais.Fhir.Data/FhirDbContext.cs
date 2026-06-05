using Microsoft.EntityFrameworkCore;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data;

/// <summary>
/// Contexto do serviço FHIR. Dono do schema <c>fhir</c> (modelo document-store:
/// uma tabela por tipo de recurso, recurso completo em coluna jsonb).
/// Serviço autônomo (ADR-0010): este DbContext não conhece o schema smsmarica.
/// </summary>
public sealed class FhirDbContext(DbContextOptions<FhirDbContext> options) : DbContext(options)
{
    public const string Schema = "fhir";

    public DbSet<PatientRow> Patients => Set<PatientRow>();
    public DbSet<PractitionerRow> Practitioners => Set<PractitionerRow>();
    public DbSet<EncounterRow> Encounters => Set<EncounterRow>();
    public DbSet<ConditionRow> Conditions => Set<ConditionRow>();
    public DbSet<DocumentReferenceRow> DocumentReferences => Set<DocumentReferenceRow>();
    public DbSet<MedicationRequestRow> MedicationRequests => Set<MedicationRequestRow>();
    public DbSet<MedicationAdministrationRow> MedicationAdministrations => Set<MedicationAdministrationRow>();
    public DbSet<ObservationRow> Observations => Set<ObservationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FhirDbContext).Assembly);
    }
}
