using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Tfd;

namespace SMSMais.Data.Configurations;

internal sealed class TfdConfiguracaoConfiguration : IEntityTypeConfiguration<TfdConfiguracao>
{
    public void Configure(EntityTypeBuilder<TfdConfiguracao> builder)
    {
        builder.ToTable("tfd_configuracao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ValorPor50Km).HasColumnName("valor_por_50km").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(c => c.KmPorUnidade).HasColumnName("km_por_unidade").HasDefaultValue(50).IsRequired();
        builder.Property(c => c.CodigoSigtap).HasColumnName("codigo_sigtap").HasMaxLength(20);
        builder.Property(c => c.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
    }
}
