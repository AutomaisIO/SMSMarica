using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PractitionerIdentifierConfiguration : IEntityTypeConfiguration<PractitionerIdentifier>
{
    public void Configure(EntityTypeBuilder<PractitionerIdentifier> b)
    {
        b.ToTable("practitioner_identifier", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PractitionerId).HasColumnName("practitioner_id").IsRequired();
        b.Property(x => x.System).HasColumnName("system").HasMaxLength(200).IsRequired();
        b.Property(x => x.Value).HasColumnName("value").HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");
        b.Property(x => x.IssuerName).HasColumnName("issuer_name").HasMaxLength(120);
        b.Property(x => x.IssuerState).HasColumnName("issuer_state").HasMaxLength(2);

        b.HasOne(x => x.Practitioner)
            .WithMany(p => p.Identifiers)
            .HasForeignKey(x => x.PractitionerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.System, x.Value })
            .IsUnique()
            .HasDatabaseName("ux_practitioner_identifier_system_value");
    }
}
