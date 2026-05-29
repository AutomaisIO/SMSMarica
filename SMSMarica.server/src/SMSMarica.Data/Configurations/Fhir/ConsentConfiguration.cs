using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    public void Configure(EntityTypeBuilder<Consent> b)
    {
        b.ToTable("consent", schema: "fhir");
        b.HasKey(c => c.Id);

        b.Property(c => c.Id).HasColumnName("id");
        b.Property(c => c.VersionId).HasColumnName("version_id").HasDefaultValue(1).IsRequired();
        b.Property(c => c.LastUpdated).HasColumnName("last_updated").IsRequired();
        b.Property(c => c.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(c => c.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        b.Property(c => c.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        b.Property(c => c.LegalBasis).HasColumnName("legal_basis").HasConversion<int>().IsRequired();
        b.Property(c => c.GrantedAt).HasColumnName("granted_at").IsRequired();
        b.Property(c => c.RevokedAt).HasColumnName("revoked_at");
        b.Property(c => c.ValidFrom).HasColumnName("valid_from");
        b.Property(c => c.ValidUntil).HasColumnName("valid_until");
        b.Property(c => c.GrantorName).HasColumnName("grantor_name").HasMaxLength(200);
        b.Property(c => c.DocumentUrl).HasColumnName("document_url").HasMaxLength(500);
        b.Property(c => c.Notes).HasColumnName("notes");
        b.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(c => c.CreatedBy).HasColumnName("created_by");
        b.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        b.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        b.HasOne(c => c.Patient)
            .WithMany(p => p.Consents)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(c => new { c.PatientId, c.Type, c.Status })
            .HasDatabaseName("ix_consent_patient_type_status");
    }
}
