using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientNameConfiguration : IEntityTypeConfiguration<PatientName>
{
    public void Configure(EntityTypeBuilder<PatientName> b)
    {
        b.ToTable("patient_name", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.Use).HasColumnName("use").HasConversion<int>().IsRequired();
        b.Property(x => x.Text).HasColumnName("text").HasMaxLength(400).IsRequired();
        b.Property(x => x.Family).HasColumnName("family").HasMaxLength(200);
        b.Property(x => x.Given).HasColumnName("given").HasColumnType("text[]").IsRequired();
        b.Property(x => x.Prefix).HasColumnName("prefix").HasColumnType("text[]").IsRequired();
        b.Property(x => x.Suffix).HasColumnName("suffix").HasColumnType("text[]").IsRequired();
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Names)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_name_patient_id");
        // Para busca por nome
        b.HasIndex(x => x.Text).HasDatabaseName("ix_patient_name_text");
    }
}
