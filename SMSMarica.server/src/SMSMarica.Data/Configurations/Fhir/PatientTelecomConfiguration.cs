using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientTelecomConfiguration : IEntityTypeConfiguration<PatientTelecom>
{
    public void Configure(EntityTypeBuilder<PatientTelecom> b)
    {
        b.ToTable("patient_telecom", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.System).HasColumnName("system").HasConversion<int>().IsRequired();
        b.Property(x => x.Value).HasColumnName("value").HasMaxLength(200).IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.Rank).HasColumnName("rank");
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Telecoms)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_telecom_patient_id");
    }
}
