using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientPhotoConfiguration : IEntityTypeConfiguration<PatientPhoto>
{
    public void Configure(EntityTypeBuilder<PatientPhoto> b)
    {
        b.ToTable("patient_photo", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(40).IsRequired();
        b.Property(x => x.DataBase64).HasColumnName("data_base64").HasColumnType("text");
        b.Property(x => x.Url).HasColumnName("url").HasMaxLength(500);
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false).IsRequired();
        b.Property(x => x.AuthorizedDisplay).HasColumnName("authorized_display").HasDefaultValue(false).IsRequired();

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Photos)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_photo_patient_id");
    }
}
