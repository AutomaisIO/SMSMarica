using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientContactConfiguration : IEntityTypeConfiguration<PatientContact>
{
    public void Configure(EntityTypeBuilder<PatientContact> b)
    {
        b.ToTable("patient_contact", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.Relationship).HasColumnName("relationship").HasConversion<int>().IsRequired();
        b.Property(x => x.RelationshipText).HasColumnName("relationship_text").HasMaxLength(120);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.TelephoneDdd).HasColumnName("telephone_ddd").HasMaxLength(4);
        b.Property(x => x.TelephoneNumber).HasColumnName("telephone_number").HasMaxLength(15);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
        b.Property(x => x.Gender).HasColumnName("gender").HasConversion<int>().IsRequired();
        b.Property(x => x.AddressLine).HasColumnName("address_line").HasMaxLength(200);
        b.Property(x => x.AddressCity).HasColumnName("address_city").HasMaxLength(120);
        b.Property(x => x.AddressState).HasColumnName("address_state").HasMaxLength(2);
        b.Property(x => x.AddressPostalCode).HasColumnName("address_postal_code").HasMaxLength(8);
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Contacts)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_contact_patient_id");
    }
}
