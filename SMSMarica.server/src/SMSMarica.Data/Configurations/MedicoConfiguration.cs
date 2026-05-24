using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class MedicoConfiguration : IEntityTypeConfiguration<Medico>
{
    public void Configure(EntityTypeBuilder<Medico> builder)
    {
        builder.ToTable("medico");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(m => m.Crm).HasColumnName("crm").HasMaxLength(15).IsRequired();
        builder.Property(m => m.UfCrm).HasColumnName("uf_crm").HasMaxLength(2).IsRequired();
        builder.Property(m => m.Especialidade).HasColumnName("especialidade").HasMaxLength(120);
        builder.Property(m => m.Rqe).HasColumnName("rqe").HasMaxLength(20);
        builder.Property(m => m.ValidadeCrm).HasColumnName("validade_crm");

        // Auditoria
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(m => m.CriadoPor).HasColumnName("criado_por");
        builder.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(m => m.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(m => m.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(m => m.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(m => m.Usuario)
            .WithOne(u => u.Medico)
            .HasForeignKey<Medico>(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.UsuarioId).IsUnique();
        builder.HasIndex(m => new { m.Crm, m.UfCrm }).IsUnique();
        builder.HasIndex(m => m.ExcluidoEm)
            .HasDatabaseName("ix_medico_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
