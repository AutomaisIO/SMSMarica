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
    public DbSet<LocationRow> Locations => Set<LocationRow>();

    /// <summary>Unidade de saúde — eixo durável do dado clínico (ADR-0039).</summary>
    public DbSet<OrganizationRow> Organizations => Set<OrganizationRow>();

    /// <summary>
    /// Normalização insensível a acento, versão <b>IMMUTABLE</b> de <c>unaccent()</c> — só
    /// traduzível em consulta EF (chamar em C# lança). Existe porque a <c>unaccent(text)</c> da
    /// extensão é <c>STABLE</c> (e pertence ao <c>postgres</c>, não dá para marcá-la IMMUTABLE),
    /// e um índice de expressão exige IMMUTABLE. A busca de paciente por nome usa
    /// <c>f_unaccent(nome) ILIKE …</c>, atendida pelo índice GIN trigram
    /// <c>ix_patient_nome_funaccent_trgm</c> (migration AddPatientTrgmSearch). A função vive no
    /// schema smsmarica, junto da unaccent que ela encapsula — mesmo acoplamento de runtime que o
    /// serviço já tem (search_path inclui smsmarica; ver Program.cs).
    /// </summary>
    public static string FUnaccent(string input) =>
        throw new NotSupportedException("f_unaccent só é traduzível em consultas EF.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FhirDbContext).Assembly);

        modelBuilder.HasDbFunction(typeof(FhirDbContext).GetMethod(nameof(FUnaccent))!)
            .HasName("f_unaccent").HasSchema("smsmarica");
    }
}
