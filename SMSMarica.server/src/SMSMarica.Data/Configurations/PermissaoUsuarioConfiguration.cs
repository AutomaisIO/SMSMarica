using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class PermissaoUsuarioConfiguration : IEntityTypeConfiguration<PermissaoUsuario>
{
    public void Configure(EntityTypeBuilder<PermissaoUsuario> builder)
    {
        builder.ToTable("permissao_usuario");
        builder.HasKey(p => new { p.UsuarioId, p.Modulo });

        builder.Property(p => p.UsuarioId).HasColumnName("usuario_id");
        builder.Property(p => p.Modulo).HasColumnName("modulo").HasConversion<int>().IsRequired();
        builder.Property(p => p.Acoes).HasColumnName("acoes").HasConversion<int>().IsRequired();

        builder.HasOne(p => p.Usuario)
            .WithMany(u => u.PermissoesOverride)
            .HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
