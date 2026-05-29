using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientLinkConfiguration : IEntityTypeConfiguration<PatientLink>
{
    public void Configure(EntityTypeBuilder<PatientLink> b)
    {
        b.ToTable("patient_link", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.OtherPatientId).HasColumnName("other_patient_id").IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("created_by");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Links)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.OtherPatient)
            .WithMany()
            .HasForeignKey(x => x.OtherPatientId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_link_patient_id");
        b.HasIndex(x => x.OtherPatientId).HasDatabaseName("ix_patient_link_other_patient_id");
    }
}
