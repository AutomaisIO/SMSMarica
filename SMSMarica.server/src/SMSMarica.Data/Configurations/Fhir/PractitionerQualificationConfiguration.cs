using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PractitionerQualificationConfiguration : IEntityTypeConfiguration<PractitionerQualification>
{
    public void Configure(EntityTypeBuilder<PractitionerQualification> b)
    {
        b.ToTable("practitioner_qualification", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PractitionerId).HasColumnName("practitioner_id").IsRequired();
        b.Property(x => x.CouncilCode).HasColumnName("council_code").HasMaxLength(20).IsRequired();
        b.Property(x => x.CouncilNumber).HasColumnName("council_number").HasMaxLength(20).IsRequired();
        b.Property(x => x.CouncilState).HasColumnName("council_state").HasMaxLength(2).IsRequired();
        b.Property(x => x.SpecialtyCode).HasColumnName("specialty_code").HasMaxLength(20);
        b.Property(x => x.SpecialtyName).HasColumnName("specialty_name").HasMaxLength(200);
        b.Property(x => x.PeriodStart).HasColumnName("period_start");
        b.Property(x => x.PeriodEnd).HasColumnName("period_end");
        b.Property(x => x.IssuerOrganizationId).HasColumnName("issuer_organization_id");

        b.HasOne(x => x.Practitioner)
            .WithMany(p => p.Qualifications)
            .HasForeignKey(x => x.PractitionerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.IssuerOrganization)
            .WithMany()
            .HasForeignKey(x => x.IssuerOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.CouncilCode, x.CouncilNumber, x.CouncilState })
            .IsUnique()
            .HasDatabaseName("ux_practitioner_qualification_council");
    }
}
