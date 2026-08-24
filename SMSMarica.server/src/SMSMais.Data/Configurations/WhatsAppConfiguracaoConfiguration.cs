using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

public class WhatsAppConfiguracaoConfiguration : IEntityTypeConfiguration<WhatsAppConfiguracao>
{
    public void Configure(EntityTypeBuilder<WhatsAppConfiguracao> builder)
    {
        builder.ToTable("whatsapp_configuracao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PhoneNumberId).HasColumnName("phone_number_id").HasMaxLength(60);
        builder.Property(c => c.ZapBaseUrl).HasColumnName("zap_base_url").HasMaxLength(200);
        builder.Property(c => c.ZapTokenCifrado).HasColumnName("zap_token_cifrado").HasMaxLength(1000);
        builder.Property(c => c.ZapSegredoWebhookCifrado).HasColumnName("zap_segredo_webhook_cifrado").HasMaxLength(1000);
        builder.Property(c => c.ZapAtivo).HasColumnName("zap_ativo").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em");
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
    }
}
