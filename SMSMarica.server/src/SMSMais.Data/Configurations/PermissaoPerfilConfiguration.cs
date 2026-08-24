using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class PermissaoPerfilConfiguration : IEntityTypeConfiguration<PermissaoPerfil>
{
    public void Configure(EntityTypeBuilder<PermissaoPerfil> builder)
    {
        builder.ToTable("permissao_perfil");
        builder.HasKey(p => new { p.PerfilId, p.Modulo });

        builder.Property(p => p.PerfilId).HasColumnName("perfil_id");
        builder.Property(p => p.Modulo).HasColumnName("modulo").HasConversion<int>().IsRequired();
        builder.Property(p => p.Acoes).HasColumnName("acoes").HasConversion<int>().IsRequired();

        builder.HasOne(p => p.Perfil)
            .WithMany(p => p.Permissoes)
            .HasForeignKey(p => p.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
