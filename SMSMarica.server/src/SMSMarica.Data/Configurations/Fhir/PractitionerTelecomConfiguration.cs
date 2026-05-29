using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PractitionerTelecomConfiguration : IEntityTypeConfiguration<PractitionerTelecom>
{
    public void Configure(EntityTypeBuilder<PractitionerTelecom> b)
    {
        b.ToTable("practitioner_telecom", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PractitionerId).HasColumnName("practitioner_id").IsRequired();
        b.Property(x => x.System).HasColumnName("system").HasConversion<int>().IsRequired();
        b.Property(x => x.Value).HasColumnName("value").HasMaxLength(200).IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.Rank).HasColumnName("rank");
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Practitioner)
            .WithMany(p => p.Telecoms)
            .HasForeignKey(x => x.PractitionerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PractitionerId).HasDatabaseName("ix_practitioner_telecom_practitioner_id");
    }
}
