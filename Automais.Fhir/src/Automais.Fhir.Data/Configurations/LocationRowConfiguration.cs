using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class LocationRowConfiguration : IEntityTypeConfiguration<LocationRow>
{
    public void Configure(EntityTypeBuilder<LocationRow> builder)
    {
        builder.ToTable("location");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(l => l.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(l => l.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(l => l.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(l => l.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(l => l.PhysicalType).HasColumnName("physical_type").HasMaxLength(10);
        builder.Property(l => l.PartOfId).HasColumnName("part_of_id");

        builder.Property(l => l.IdentifierSystem).HasColumnName("identifier_system").HasMaxLength(100);
        builder.Property(l => l.IdentifierValue).HasColumnName("identifier_value").HasMaxLength(200);

        builder.HasIndex(l => l.PartOfId).HasFilter("part_of_id IS NOT NULL");
        builder.HasIndex(l => l.MetaSource);

        // Conditional update (ADR-0024): no máximo UMA linha viva por identifier de negócio.
        builder.HasIndex(l => new { l.IdentifierSystem, l.IdentifierValue })
            .IsUnique()
            .HasFilter("identifier_system IS NOT NULL AND NOT is_deleted");
    }
}
