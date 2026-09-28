using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Telefonia;

namespace SMSMais.Data.Configurations;

public class UsuarioSoftphoneConfiguration : IEntityTypeConfiguration<UsuarioSoftphone>
{
    public void Configure(EntityTypeBuilder<UsuarioSoftphone> builder)
    {
        builder.ToTable("usuario_softphone");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UsuarioId).HasColumnName("usuario_id");
        builder.Property(s => s.Ramal).HasColumnName("ramal").HasMaxLength(10).IsRequired();
        builder.Property(s => s.NomeExibicao).HasColumnName("nome_exibicao").HasMaxLength(60).IsRequired();
        builder.Property(s => s.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(s => s.CriadoEm).HasColumnName("criado_em");
        builder.Property(s => s.CriadoPor).HasColumnName("criado_por");
        builder.Property(s => s.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(s => s.AtualizadoPor).HasColumnName("atualizado_por");

        builder.HasIndex(s => s.UsuarioId).IsUnique().HasDatabaseName("ux_usuario_softphone_usuario");
        builder.HasIndex(s => s.Ramal).IsUnique().HasDatabaseName("ux_usuario_softphone_ramal");

        builder.HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
