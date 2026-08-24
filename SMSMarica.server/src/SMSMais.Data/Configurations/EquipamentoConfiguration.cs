using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class EquipamentoConfiguration : IEntityTypeConfiguration<Equipamento>
{
    public void Configure(EntityTypeBuilder<Equipamento> builder)
    {
        builder.ToTable("equipamento");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(e => e.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(e => e.ModalidadeDicom).HasColumnName("modalidade_dicom").HasConversion<int>().IsRequired();
        builder.Property(e => e.IdentificadorDicom).HasColumnName("identificador_dicom").HasMaxLength(64);
        builder.Property(e => e.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por");
        builder.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(e => e.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(e => e.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(e => e.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(e => e.Unidade)
            .WithMany()
            .HasForeignKey(e => e.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UnidadeId, e.Nome })
            .HasDatabaseName("ux_equipamento_unidade_nome")
            .IsUnique()
            .HasFilter("excluido_em IS NULL");

        builder.HasIndex(e => e.ExcluidoEm)
            .HasDatabaseName("ix_equipamento_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
