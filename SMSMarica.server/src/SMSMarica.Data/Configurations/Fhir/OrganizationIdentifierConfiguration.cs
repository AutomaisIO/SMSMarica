using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class OrganizationIdentifierConfiguration : IEntityTypeConfiguration<OrganizationIdentifier>
{
    public void Configure(EntityTypeBuilder<OrganizationIdentifier> b)
    {
        b.ToTable("organization_identifier", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
        b.Property(x => x.System).HasColumnName("system").HasMaxLength(200).IsRequired();
        b.Property(x => x.Value).HasColumnName("value").HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Organization)
            .WithMany(o => o.Identifiers)
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.System, x.Value })
            .IsUnique()
            .HasDatabaseName("ux_organization_identifier_system_value");
    }
}
