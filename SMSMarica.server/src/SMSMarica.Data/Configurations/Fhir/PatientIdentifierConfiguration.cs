using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientIdentifierConfiguration : IEntityTypeConfiguration<PatientIdentifier>
{
    public void Configure(EntityTypeBuilder<PatientIdentifier> b)
    {
        b.ToTable("patient_identifier", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.System).HasColumnName("system").HasMaxLength(200).IsRequired();
        b.Property(x => x.Value).HasColumnName("value").HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");
        b.Property(x => x.IssuerName).HasColumnName("issuer_name").HasMaxLength(120);
        b.Property(x => x.IssuerState).HasColumnName("issuer_state").HasMaxLength(2);
        b.Property(x => x.RegistryName).HasColumnName("registry_name").HasMaxLength(200);
        b.Property(x => x.RegistryBook).HasColumnName("registry_book").HasMaxLength(40);
        b.Property(x => x.RegistryPage).HasColumnName("registry_page").HasMaxLength(40);
        b.Property(x => x.RegistryTerm).HasColumnName("registry_term").HasMaxLength(40);
        b.Property(x => x.AssignerOrganizationId).HasColumnName("assigner_organization_id");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Identifiers)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AssignerOrganization)
            .WithMany()
            .HasForeignKey(x => x.AssignerOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unicidade do par (System, Value) — não dois identificadores idênticos no banco
        b.HasIndex(x => new { x.System, x.Value })
            .IsUnique()
            .HasDatabaseName("ux_patient_identifier_system_value");

        // Busca rápida por paciente
        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_identifier_patient_id");
    }
}
