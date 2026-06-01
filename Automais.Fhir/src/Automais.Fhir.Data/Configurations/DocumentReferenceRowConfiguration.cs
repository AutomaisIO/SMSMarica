using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class DocumentReferenceRowConfiguration : IEntityTypeConfiguration<DocumentReferenceRow>
{
    public void Configure(EntityTypeBuilder<DocumentReferenceRow> builder)
    {
        builder.ToTable("document_reference");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(d => d.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(d => d.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(d => d.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(d => d.PatientId).HasColumnName("patient_id");
        builder.Property(d => d.EncounterId).HasColumnName("encounter_id");
        builder.Property(d => d.Tipo).HasColumnName("tipo").HasMaxLength(200);
        builder.Property(d => d.Data).HasColumnName("data");

        builder.HasIndex(d => d.PatientId).HasFilter("patient_id IS NOT NULL");
        builder.HasIndex(d => d.EncounterId).HasFilter("encounter_id IS NOT NULL");
        builder.HasIndex(d => d.MetaSource);
    }
}
