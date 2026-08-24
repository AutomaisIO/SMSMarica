using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class AnexoUploadTokenConfiguration : IEntityTypeConfiguration<AnexoUploadToken>
{
    public void Configure(EntityTypeBuilder<AnexoUploadToken> builder)
    {
        builder.ToTable("anexo_upload_token");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(120).IsRequired();
        builder.Property(t => t.ExameImagemId).HasColumnName("exame_imagem_id").IsRequired();
        builder.Property(t => t.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(t => t.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200).IsRequired();

        builder.Property(t => t.CriadoPor).HasColumnName("criado_por");
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(t => t.RevogadoEm).HasColumnName("revogado_em");
        builder.Property(t => t.UltimoUsoEm).HasColumnName("ultimo_uso_em");

        builder.HasOne(t => t.ExameImagem)
            .WithMany()
            .HasForeignKey(t => t.ExameImagemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Segredo aleatório, nunca reaproveitado → índice único simples.
        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("ix_anexo_upload_token_token");

        builder.HasIndex(t => t.ExameImagemId);
    }
}
