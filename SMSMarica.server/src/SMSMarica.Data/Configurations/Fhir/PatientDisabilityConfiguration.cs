using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientDisabilityConfiguration : IEntityTypeConfiguration<PatientDisability>
{
    public void Configure(EntityTypeBuilder<PatientDisability> b)
    {
        b.ToTable("patient_disability", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(400);
        b.Property(x => x.CidCode).HasColumnName("cid_code").HasMaxLength(10);
        b.Property(x => x.StartDate).HasColumnName("start_date");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Disabilities)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_disability_patient_id");
    }
}
