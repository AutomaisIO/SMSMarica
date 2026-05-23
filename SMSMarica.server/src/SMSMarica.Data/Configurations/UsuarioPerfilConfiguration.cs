using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class UsuarioPerfilConfiguration : IEntityTypeConfiguration<UsuarioPerfil>
{
    public void Configure(EntityTypeBuilder<UsuarioPerfil> builder)
    {
        builder.ToTable("usuario_perfil");
        builder.HasKey(up => new { up.UsuarioId, up.PerfilId });

        builder.Property(up => up.UsuarioId).HasColumnName("usuario_id");
        builder.Property(up => up.PerfilId).HasColumnName("perfil_id");

        builder.HasOne(up => up.Usuario)
            .WithMany(u => u.UsuariosPerfis)
            .HasForeignKey(up => up.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.Perfil)
            .WithMany(p => p.UsuariosPerfis)
            .HasForeignKey(up => up.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
