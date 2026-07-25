using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Data.Configurations.Sisreg;

internal sealed class SisregCredencialUnidadeConfiguration : IEntityTypeConfiguration<SisregCredencialUnidade>
{
    public void Configure(EntityTypeBuilder<SisregCredencialUnidade> builder)
    {
        builder.ToTable("sisreg_credencial_unidade");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.Usuario).HasColumnName("usuario").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SenhaCifrada).HasColumnName("senha_cifrada").IsRequired();
        builder.Property(x => x.CnesConfirmado).HasColumnName("cnes_confirmado").HasMaxLength(20);
        builder.Property(x => x.UnidadeSisregNome).HasColumnName("unidade_sisreg_nome").HasMaxLength(300);
        builder.Property(x => x.ValidadoEm).HasColumnName("validado_em");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");

        // Uma credencial por unidade.
        builder.HasIndex(x => x.UnidadeId)
            .IsUnique()
            .HasDatabaseName("ux_sisreg_credencial_unidade");

        builder.HasOne<Entities.Unidade>()
            .WithMany()
            .HasForeignKey(x => x.UnidadeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
