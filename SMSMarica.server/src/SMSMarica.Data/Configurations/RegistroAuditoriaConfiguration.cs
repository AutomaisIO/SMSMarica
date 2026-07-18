using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("registro_auditoria");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Entidade).HasColumnName("entidade").HasMaxLength(100).IsRequired();
        builder.Property(r => r.EntidadeId).HasColumnName("entidade_id").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Acao).HasColumnName("acao").HasMaxLength(60).IsRequired();

        // Valores simples (ex.: nome). text, não jsonb — não é JSON válido.
        builder.Property(r => r.ValorAnterior).HasColumnName("valor_anterior").HasColumnType("text");
        builder.Property(r => r.ValorNovo).HasColumnName("valor_novo").HasColumnType("text");

        builder.Property(r => r.UsuarioId).HasColumnName("usuario_id");
        builder.Property(r => r.UsuarioNome).HasColumnName("usuario_nome").HasMaxLength(200);
        builder.Property(r => r.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(r => r.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(r => new { r.Entidade, r.EntidadeId })
            .HasDatabaseName("ix_registro_auditoria_entidade");
        builder.HasIndex(r => r.CriadoEm)
            .HasDatabaseName("ix_registro_auditoria_criado_em");
        builder.HasIndex(r => r.UsuarioId)
            .HasDatabaseName("ix_registro_auditoria_usuario_id");
    }
}
