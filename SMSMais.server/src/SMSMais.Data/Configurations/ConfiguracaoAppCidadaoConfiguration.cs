using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class ConfiguracaoAppCidadaoConfiguration : IEntityTypeConfiguration<ConfiguracaoAppCidadao>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoAppCidadao> builder)
    {
        builder.ToTable("configuracao_app_cidadao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PwaConsultarEsus).HasColumnName("pwa_consultar_esus").IsRequired();
        builder.Property(c => c.NativoConsultarEsus).HasColumnName("nativo_consultar_esus").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
