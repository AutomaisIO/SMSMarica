using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("organization", schema: "fhir");
        b.HasKey(o => o.Id);

        b.Property(o => o.Id).HasColumnName("id");
        b.Property(o => o.VersionId).HasColumnName("version_id").HasDefaultValue(1).IsRequired();
        b.Property(o => o.LastUpdated).HasColumnName("last_updated").IsRequired();
        b.Property(o => o.Active).HasColumnName("active").HasDefaultValue(true).IsRequired();
        b.Property(o => o.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(o => o.Alias).HasColumnName("alias").HasMaxLength(400);
        b.Property(o => o.PartOfId).HasColumnName("part_of_id");

        b.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(o => o.CreatedBy).HasColumnName("created_by");
        b.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        b.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        b.Property(o => o.DeletedAt).HasColumnName("deleted_at");
        b.Property(o => o.DeletedBy).HasColumnName("deleted_by");

        b.HasOne(o => o.PartOf)
            .WithMany()
            .HasForeignKey(o => o.PartOfId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(o => o.DeletedAt)
            .HasDatabaseName("ix_organization_deleted_at")
            .HasFilter("deleted_at IS NULL");
    }
}
