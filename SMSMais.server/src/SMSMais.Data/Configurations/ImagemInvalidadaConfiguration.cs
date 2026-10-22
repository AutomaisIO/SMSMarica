using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class ImagemInvalidadaConfiguration : IEntityTypeConfiguration<ImagemInvalidada>
{
    public void Configure(EntityTypeBuilder<ImagemInvalidada> builder)
    {
        builder.ToTable("imagem_invalidada");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.StudyInstanceUID)
            .HasColumnName("study_instance_uid")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(i => i.SopInstanceUID)
            .HasColumnName("sop_instance_uid")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(i => i.Motivo).HasColumnName("motivo").HasMaxLength(500);
        builder.Property(i => i.CriadoPor).HasColumnName("criado_por").IsRequired();
        builder.Property(i => i.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(i => i.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(i => i.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(i => i.CriadoPorUsuario)
            .WithMany()
            .HasForeignKey(i => i.CriadoPor)
            .OnDelete(DeleteBehavior.Restrict);

        // Uma invalidação VIGENTE por imagem; as restauradas ficam como trilha.
        builder.HasIndex(i => new { i.StudyInstanceUID, i.SopInstanceUID })
            .IsUnique()
            .HasFilter("excluido_em IS NULL");
        // "Quais imagens deste estudo estão fora?" — a consulta de todo PDF.
        builder.HasIndex(i => i.StudyInstanceUID);
    }
}
