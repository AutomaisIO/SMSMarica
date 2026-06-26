using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class ExameAssociacaoConfiguration : IEntityTypeConfiguration<ExameAssociacao>
{
    public void Configure(EntityTypeBuilder<ExameAssociacao> builder)
    {
        builder.ToTable("exame_associacao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.StudyInstanceUID).HasColumnName("study_instance_uid").HasMaxLength(128).IsRequired();
        builder.Property(a => a.SolicitacaoExameId).HasColumnName("solicitacao_exame_id").IsRequired();
        builder.Property(a => a.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(a => a.AccessionNumberDicomOriginal).HasColumnName("accession_number_dicom_original").HasMaxLength(64);
        builder.Property(a => a.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(a => a.StatusSolicitacaoAnterior).HasColumnName("status_solicitacao_anterior").HasConversion<int>();

        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(a => a.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(a => a.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(a => a.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(a => a.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // FK smsmarica → smsmarica (PacienteId referencia fhir.patient — sem FK local).
        builder.HasOne(a => a.SolicitacaoExame)
            .WithMany()
            .HasForeignKey(a => a.SolicitacaoExameId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1 associação ATIVA por estudo (soft-delete libera o slot para reassociar).
        builder.HasIndex(a => a.StudyInstanceUID)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_exame_associacao_study_uid_ativa");

        builder.HasIndex(a => a.SolicitacaoExameId).HasDatabaseName("ix_exame_associacao_solicitacao");
        builder.HasIndex(a => a.PacienteId).HasDatabaseName("ix_exame_associacao_paciente");
    }
}
