using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class EncounterRowConfiguration : IEntityTypeConfiguration<EncounterRow>
{
    public void Configure(EntityTypeBuilder<EncounterRow> builder)
    {
        builder.ToTable("encounter");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(e => e.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(e => e.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(e => e.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(e => e.PatientId).HasColumnName("patient_id");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(e => e.Classe).HasColumnName("classe").HasMaxLength(10);
        builder.Property(e => e.PeriodStart).HasColumnName("period_start");

        builder.HasIndex(e => e.PatientId).HasFilter("patient_id IS NOT NULL");
        builder.HasIndex(e => e.PeriodStart);
        builder.HasIndex(e => e.MetaSource);
    }
}
