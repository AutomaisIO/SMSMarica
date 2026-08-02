using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class ObservationRowConfiguration : IEntityTypeConfiguration<ObservationRow>
{
    public void Configure(EntityTypeBuilder<ObservationRow> builder)
    {
        builder.ToTable("observation");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");

        builder.Property(o => o.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(o => o.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(o => o.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(o => o.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(o => o.PatientId).HasColumnName("patient_id");
        builder.Property(o => o.EncounterId).HasColumnName("encounter_id");
        builder.Property(o => o.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(o => o.Effective).HasColumnName("effective");

        builder.HasIndex(o => o.PatientId).HasFilter("patient_id IS NOT NULL");
        builder.HasIndex(o => o.EncounterId).HasFilter("encounter_id IS NOT NULL");
        builder.HasIndex(o => o.Code).HasFilter("code IS NOT NULL");
        builder.HasIndex(o => o.MetaSource);
        builder.Property(x => x.IdentifierSystem).HasColumnName("identifier_system").HasMaxLength(100);
        builder.Property(x => x.IdentifierValue).HasColumnName("identifier_value").HasMaxLength(200);

        // Conditional update (ADR-0024): no máximo UMA linha viva por identifier de negócio.
        builder.HasIndex(x => new { x.IdentifierSystem, x.IdentifierValue })
            .IsUnique()
            .HasFilter("identifier_system IS NOT NULL AND NOT is_deleted");
    }
}
