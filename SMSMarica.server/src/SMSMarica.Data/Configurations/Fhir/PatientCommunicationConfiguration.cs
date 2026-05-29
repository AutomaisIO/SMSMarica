using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientCommunicationConfiguration : IEntityTypeConfiguration<PatientCommunication>
{
    public void Configure(EntityTypeBuilder<PatientCommunication> b)
    {
        b.ToTable("patient_communication", schema: "fhir");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        b.Property(x => x.LanguageCode).HasColumnName("language_code").HasMaxLength(20).IsRequired();
        b.Property(x => x.Preferred).HasColumnName("preferred").HasDefaultValue(false).IsRequired();
        b.Property(x => x.BarreiraComunicacaoCodigo).HasColumnName("barreira_comunicacao_codigo");

        b.HasOne(x => x.Patient)
            .WithMany(p => p.Communications)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.BarreiraComunicacao)
            .WithMany()
            .HasForeignKey(x => x.BarreiraComunicacaoCodigo)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.PatientId).HasDatabaseName("ix_patient_communication_patient_id");
    }
}
