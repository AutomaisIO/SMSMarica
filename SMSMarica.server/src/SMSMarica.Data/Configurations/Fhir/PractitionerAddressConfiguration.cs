using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PractitionerAddressConfiguration : IEntityTypeConfiguration<PractitionerAddress>
{
    public void Configure(EntityTypeBuilder<PractitionerAddress> b)
    {
        b.ToTable("practitioner_address", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PractitionerId).HasColumnName("practitioner_id").IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(x => x.Text).HasColumnName("text").HasMaxLength(500);
        b.Property(x => x.Line1).HasColumnName("line1").HasMaxLength(200);
        b.Property(x => x.Line2).HasColumnName("line2").HasMaxLength(200);
        b.Property(x => x.District).HasColumnName("district").HasMaxLength(120);
        b.Property(x => x.MunicipioCodigo).HasColumnName("municipio_codigo");
        b.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        b.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(8);
        b.Property(x => x.Country).HasColumnName("country").HasMaxLength(3).IsRequired();
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Practitioner)
            .WithMany(p => p.Addresses)
            .HasForeignKey(x => x.PractitionerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Municipio)
            .WithMany()
            .HasForeignKey(x => x.MunicipioCodigo)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.PractitionerId).HasDatabaseName("ix_practitioner_address_practitioner_id");
    }
}
