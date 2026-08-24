using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class AssinaturaMedicoConfiguration : IEntityTypeConfiguration<AssinaturaMedico>
{
    public void Configure(EntityTypeBuilder<AssinaturaMedico> builder)
    {
        builder.ToTable("assinatura_medico");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.MedicoId).HasColumnName("medico_id").IsRequired();
        builder.Property(a => a.ImagemBase64).HasColumnName("imagem_base64").HasColumnType("text").IsRequired();
        builder.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(60).IsRequired();
        builder.Property(a => a.Formato).HasColumnName("formato").HasConversion<int>().IsRequired();

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

        // 1 assinatura ativa por médico (soft-delete libera o slot).
        builder.HasIndex(a => a.MedicoId)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_assinatura_medico_medico_id_ativa");
    }
}
