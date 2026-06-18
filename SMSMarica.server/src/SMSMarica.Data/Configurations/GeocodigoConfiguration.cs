using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Data.Configurations;

internal sealed class GeocodigoConfiguration : IEntityTypeConfiguration<Geocodigo>
{
    public void Configure(EntityTypeBuilder<Geocodigo> builder)
    {
        builder.ToTable("tfd_geocodigo");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
        builder.Property(g => g.EnderecoNormalizado).HasColumnName("endereco_normalizado").HasMaxLength(400).IsRequired();
        builder.Property(g => g.Latitude).HasColumnName("latitude").IsRequired();
        builder.Property(g => g.Longitude).HasColumnName("longitude").IsRequired();
        builder.Property(g => g.Fonte).HasColumnName("fonte").HasConversion<int>().IsRequired();
        builder.Property(g => g.Precisao).HasColumnName("precisao").HasMaxLength(40);
        builder.Property(g => g.RevisaoPendente).HasColumnName("revisao_pendente").HasDefaultValue(false).IsRequired();
        builder.Property(g => g.GeocodificadoEm).HasColumnName("geocodificado_em").IsRequired();

        builder.HasIndex(g => g.Hash).IsUnique();
        builder.HasIndex(g => g.RevisaoPendente);
    }
}
