using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class UnidadePesquisaConfigConfiguration : IEntityTypeConfiguration<UnidadePesquisaConfig>
{
    public void Configure(EntityTypeBuilder<UnidadePesquisaConfig> builder)
    {
        builder.ToTable("unidade_pesquisa_config");
        builder.HasKey(c => c.UnidadeId);

        builder.Property(c => c.UnidadeId).HasColumnName("unidade_id");
        builder.Property(c => c.EnvioWhatsAppAtivo)
            .HasColumnName("envio_whatsapp_ativo").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.LinkResponder).HasColumnName("link_responder").HasMaxLength(500);
        builder.Property(c => c.LinkPainel).HasColumnName("link_painel").HasMaxLength(500);
        builder.Property(c => c.HorasAposAtendimento)
            .HasColumnName("horas_apos_atendimento").IsRequired().HasDefaultValue(24);
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
