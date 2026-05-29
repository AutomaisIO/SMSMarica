using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PractitionerConfiguration : IEntityTypeConfiguration<Practitioner>
{
    public void Configure(EntityTypeBuilder<Practitioner> b)
    {
        b.ToTable("practitioner", schema: "fhir");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnName("id");
        b.Property(p => p.VersionId).HasColumnName("version_id").HasDefaultValue(1).IsRequired();
        b.Property(p => p.LastUpdated).HasColumnName("last_updated").IsRequired();
        b.Property(p => p.Active).HasColumnName("active").HasDefaultValue(true).IsRequired();
        b.Property(p => p.Gender).HasColumnName("gender").HasConversion<int>().IsRequired();
        b.Property(p => p.BirthDate).HasColumnName("birth_date");

        b.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(p => p.CreatedBy).HasColumnName("created_by");
        b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        b.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        b.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        b.Property(p => p.DeletedBy).HasColumnName("deleted_by");

        b.HasIndex(p => p.DeletedAt)
            .HasDatabaseName("ix_practitioner_deleted_at")
            .HasFilter("deleted_at IS NULL");
    }
}
