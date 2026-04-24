using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class AssentoConfiguration : IEntityTypeConfiguration<Assento>
{
    public void Configure(EntityTypeBuilder<Assento> builder)
    {
        builder.ToTable("veiculo_assento");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.FileiraId).HasColumnName("fileira_id").IsRequired();
        builder.Property(a => a.Numero).HasColumnName("numero").IsRequired();
        builder.Property(a => a.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(a => a.Bloqueado).HasColumnName("bloqueado").IsRequired().HasDefaultValue(false);
        builder.Property(a => a.Excluido).HasColumnName("excluido").IsRequired().HasDefaultValue(false);
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Índice único filtrado: assentos soft-deleted não disputam a posição
        builder.HasIndex(a => new { a.FileiraId, a.Numero })
            .IsUnique()
            .HasFilter("excluido = false");
    }
}
