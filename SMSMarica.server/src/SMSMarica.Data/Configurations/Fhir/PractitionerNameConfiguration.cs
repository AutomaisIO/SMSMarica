using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PractitionerNameConfiguration : IEntityTypeConfiguration<PractitionerName>
{
    public void Configure(EntityTypeBuilder<PractitionerName> b)
    {
        b.ToTable("practitioner_name", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PractitionerId).HasColumnName("practitioner_id").IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.Text).HasColumnName("text").HasMaxLength(400).IsRequired();
        b.Property(x => x.Family).HasColumnName("family").HasMaxLength(200);
        b.Property(x => x.Given).HasColumnName("given").HasColumnType("text[]").IsRequired();
        b.Property(x => x.Prefix).HasColumnName("prefix").HasColumnType("text[]").IsRequired();
        b.Property(x => x.Suffix).HasColumnName("suffix").HasColumnType("text[]").IsRequired();
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Practitioner)
            .WithMany(p => p.Names)
            .HasForeignKey(x => x.PractitionerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PractitionerId).HasDatabaseName("ix_practitioner_name_practitioner_id");
        b.HasIndex(x => x.Text).HasDatabaseName("ix_practitioner_name_text");
    }
}
