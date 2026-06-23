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

        builder.Property(l => l.PacienteId).HasColumnName("paciente_id");
        builder.Property(l => l.MedicoId).HasColumnName("medico_id").IsRequired();
        builder.Property(l => l.LaudoTemplateId).HasColumnName("laudo_template_id");

        builder.Property(l => l.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(l => l.ConteudoJson).HasColumnName("conteudo_json").HasColumnType("jsonb").IsRequired();
        builder.Property(l => l.ConteudoHtml).HasColumnName("conteudo_html").HasColumnType("text").IsRequired();
        builder.Property(l => l.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(l => l.BiRads).HasColumnName("bi_rads").HasMaxLength(4);
        builder.Property(l => l.BiRadsSugerido).HasColumnName("bi_rads_sugerido").HasMaxLength(4);
        builder.Property(l => l.RespostasChecklist).HasColumnName("respostas_checklist").HasColumnType("jsonb");

        builder.Property(l => l.MedicoNomeSnapshot).HasColumnName("medico_nome_snapshot").HasMaxLength(200);
        builder.Property(l => l.MedicoCrmSnapshot).HasColumnName("medico_crm_snapshot").HasMaxLength(20);
        builder.Property(l => l.MedicoUfCrmSnapshot).HasColumnName("medico_uf_crm_snapshot").HasMaxLength(2);
        builder.Property(l => l.MedicoRqeSnapshot).HasColumnName("medico_rqe_snapshot").HasMaxLength(40);

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

        // PacienteId → fhir.patient, MedicoId → fhir.practitioner (hub FHIR). Sem FK local.
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
        builder.HasIndex(l => l.PacienteId);
        builder.HasIndex(l => l.MedicoId);
        builder.HasIndex(l => l.Status);

        // Busca por categoria BI-RADS (só linhas avaliadas e não excluídas).
        builder.HasIndex(l => new { l.BiRads, l.Status })
            .HasFilter("bi_rads IS NOT NULL AND excluido = false");
    }
}
