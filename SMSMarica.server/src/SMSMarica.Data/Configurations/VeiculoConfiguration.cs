using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class VeiculoConfiguration : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> builder)
    {
        builder.ToTable("veiculo");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.Placa).HasColumnName("placa").HasMaxLength(10).IsRequired();
        builder.Property(v => v.Modelo).HasColumnName("modelo").HasMaxLength(100).IsRequired();
        builder.Property(v => v.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(v => v.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(v => v.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(v => v.Placa).IsUnique();

        builder.HasMany(v => v.Fileiras)
            .WithOne(f => f.Veiculo)
            .HasForeignKey(f => f.VeiculoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
