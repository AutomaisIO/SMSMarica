using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class ConditionRowConfiguration : IEntityTypeConfiguration<ConditionRow>
{
    public void Configure(EntityTypeBuilder<ConditionRow> builder)
    {
        builder.ToTable("condition");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(c => c.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(c => c.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(c => c.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(c => c.PatientId).HasColumnName("patient_id");
        builder.Property(c => c.EncounterId).HasColumnName("encounter_id");
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(20);

        builder.HasIndex(c => c.PatientId).HasFilter("patient_id IS NOT NULL");
        builder.HasIndex(c => c.EncounterId).HasFilter("encounter_id IS NOT NULL");
        builder.HasIndex(c => c.MetaSource);
    }
}
