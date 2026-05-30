using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class LaudoConfiguration : IEntityTypeConfiguration<Laudo>
{
    public void Configure(EntityTypeBuilder<Laudo> builder)
    {
        builder.ToTable("laudo");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.StudyInstanceUID)
            .HasColumnName("study_instance_uid")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(l => l.Versao).HasColumnName("versao").IsRequired();
        builder.Property(l => l.LaudoAnteriorId).HasColumnName("laudo_anterior_id");

        builder.Property(l => l.PatientId).HasColumnName("patient_id");
        builder.Property(l => l.PractitionerId).HasColumnName("practitioner_id").IsRequired();
        builder.Property(l => l.LaudoTemplateId).HasColumnName("laudo_template_id");

        builder.Property(l => l.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(l => l.ConteudoJson).HasColumnName("conteudo_json").HasColumnType("jsonb").IsRequired();
        builder.Property(l => l.ConteudoHtml).HasColumnName("conteudo_html").HasColumnType("text").IsRequired();
        builder.Property(l => l.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(l => l.PractitionerNomeSnapshot).HasColumnName("practitioner_nome_snapshot").HasMaxLength(200);
        builder.Property(l => l.PractitionerCrmSnapshot).HasColumnName("practitioner_crm_snapshot").HasMaxLength(20);
        builder.Property(l => l.PractitionerUfCrmSnapshot).HasColumnName("practitioner_uf_crm_snapshot").HasMaxLength(2);
        builder.Property(l => l.PractitionerRqeSnapshot).HasColumnName("practitioner_rqe_snapshot").HasMaxLength(40);

        builder.Property(l => l.FinalizadoEm).HasColumnName("finalizado_em");
        builder.Property(l => l.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(l => l.AtualizadoEm).HasColumnName("atualizado_em");

        builder.Property(l => l.Excluido).HasColumnName("excluido").HasDefaultValue(false).IsRequired();
        builder.Property(l => l.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(l => l.ExcluidoPorUsuarioId).HasColumnName("excluido_por_usuario_id");

        // xmin nativo do Postgres para concorrência otimista.
        builder.Property(l => l.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(l => l.LaudoAnterior)
            .WithMany()
            .HasForeignKey(l => l.LaudoAnteriorId)
            .OnDelete(DeleteBehavior.NoAction);

        // Cross-schema FK: smsmarica.laudo.patient_id → fhir.patient.id (nullable)
        builder.HasOne(l => l.Patient)
            .WithMany()
            .HasForeignKey(l => l.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cross-schema FK: smsmarica.laudo.practitioner_id → fhir.practitioner.id
        builder.HasOne(l => l.Practitioner)
            .WithMany()
            .HasForeignKey(l => l.PractitionerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.LaudoTemplate)
            .WithMany()
            .HasForeignKey(l => l.LaudoTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Versão única por estudo entre não-excluídos (filtered index Postgres).
        builder.HasIndex(l => new { l.StudyInstanceUID, l.Versao })
            .IsUnique()
            .HasFilter("excluido = false");

        // Lookup batch para `GET /laudos?studyUIDs=...`.
        builder.HasIndex(l => l.StudyInstanceUID);
        builder.HasIndex(l => l.PatientId);
        builder.HasIndex(l => l.PractitionerId);
        builder.HasIndex(l => l.Status);
    }
}
