using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class MensageriaConfiguracaoConfiguration : IEntityTypeConfiguration<MensageriaConfiguracao>
{
    public void Configure(EntityTypeBuilder<MensageriaConfiguracao> builder)
    {
        builder.ToTable("mensageria_configuracao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TarifaUtilityUsd).HasColumnName("tarifa_utility_usd").HasPrecision(10, 5);
        builder.Property(x => x.TarifaMarketingUsd).HasColumnName("tarifa_marketing_usd").HasPrecision(10, 5);
        builder.Property(x => x.TarifaAuthenticationUsd).HasColumnName("tarifa_authentication_usd").HasPrecision(10, 5);
        builder.Property(x => x.TemplatesCategoriasJson).HasColumnName("templates_categorias_json").HasColumnType("jsonb");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
