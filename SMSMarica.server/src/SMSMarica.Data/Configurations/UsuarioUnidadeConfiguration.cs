using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Conversas;

namespace SMSMarica.Data.Configurations;

internal sealed class UsuarioUnidadeConfiguration : IEntityTypeConfiguration<UsuarioUnidade>
{
    public void Configure(EntityTypeBuilder<UsuarioUnidade> builder)
    {
        builder.ToTable("usuario_unidade");
        builder.HasKey(x => new { x.UsuarioId, x.UnidadeId });

        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id");
        builder.Property(x => x.Principal).HasColumnName("principal").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Unidade)
            .WithMany()
            .HasForeignKey(x => x.UnidadeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UnidadeId);

        // No máximo uma unidade principal por usuário.
        builder.HasIndex(x => x.UsuarioId).IsUnique().HasFilter("principal = true");
    }
}
