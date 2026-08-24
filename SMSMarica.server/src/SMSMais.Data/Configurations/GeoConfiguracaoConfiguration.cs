using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Geo;

namespace SMSMais.Data.Configurations;

internal sealed class GeoConfiguracaoConfiguration : IEntityTypeConfiguration<GeoConfiguracao>
{
    public void Configure(EntityTypeBuilder<GeoConfiguracao> builder)
    {
        builder.ToTable("geo_configuracao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.BaseUrl).HasColumnName("base_url").HasMaxLength(200).IsRequired();
        builder.Property(c => c.ApiKeyCifrada).HasColumnName("api_key_cifrada");
        builder.Property(c => c.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
    }
}
