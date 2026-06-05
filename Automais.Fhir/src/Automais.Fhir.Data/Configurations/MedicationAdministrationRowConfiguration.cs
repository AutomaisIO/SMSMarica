using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class MedicationAdministrationRowConfiguration : IEntityTypeConfiguration<MedicationAdministrationRow>
{
    public void Configure(EntityTypeBuilder<MedicationAdministrationRow> builder)
    {
        builder.ToTable("medication_administration");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(m => m.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(m => m.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(m => m.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(m => m.PatientId).HasColumnName("patient_id");
        builder.Property(m => m.EncounterId).HasColumnName("encounter_id");
        builder.Property(m => m.Medicamento).HasColumnName("medicamento").HasMaxLength(300);
        builder.Property(m => m.Effective).HasColumnName("effective");

        builder.HasIndex(m => m.PatientId).HasFilter("patient_id IS NOT NULL");
        builder.HasIndex(m => m.EncounterId).HasFilter("encounter_id IS NOT NULL");
        builder.HasIndex(m => m.MetaSource);
    }
}
