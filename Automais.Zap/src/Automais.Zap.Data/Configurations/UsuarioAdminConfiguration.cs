using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class UsuarioAdminConfiguration : IEntityTypeConfiguration<UsuarioAdmin>
{
    public void Configure(EntityTypeBuilder<UsuarioAdmin> b)
    {
        b.ToTable("usuario_admin");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        b.Property(x => x.SenhaHash).HasColumnName("senha_hash").HasMaxLength(400).IsRequired();
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.UltimoAcessoEm).HasColumnName("ultimo_acesso_em");

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_usuario_admin_email");
    }
}
