using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class UsuarioTenantConfiguration : IEntityTypeConfiguration<UsuarioTenant>
{
    public void Configure(EntityTypeBuilder<UsuarioTenant> b)
    {
        b.ToTable("usuario_tenant");
        b.HasKey(x => new { x.UsuarioId, x.TenantId });
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");

        b.HasOne(x => x.Usuario).WithMany(u => u.Tenants)
            .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Tenant).WithMany(t => t.Usuarios)
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
