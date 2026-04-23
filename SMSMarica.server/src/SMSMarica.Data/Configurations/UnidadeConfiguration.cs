using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class UnidadeConfiguration : IEntityTypeConfiguration<Unidade>
{
    public void Configure(EntityTypeBuilder<Unidade> builder)
    {
        builder.ToTable("unidade");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Endereco).HasColumnName("endereco").HasMaxLength(500).IsRequired();
        builder.Property(u => u.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(u => u.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(u => u.AtualizadoEm).HasColumnName("atualizado_em");

        builder.OwnsOne(u => u.Gps, gps =>
        {
            gps.Property(g => g.Latitude).HasColumnName("latitude").IsRequired();
            gps.Property(g => g.Longitude).HasColumnName("longitude").IsRequired();
        });
    }
}
