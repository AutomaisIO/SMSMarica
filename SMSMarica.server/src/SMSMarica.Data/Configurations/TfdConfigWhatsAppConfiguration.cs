using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Data.Configurations;

internal sealed class TfdConfigWhatsAppConfiguration : IEntityTypeConfiguration<TfdConfigWhatsApp>
{
    public void Configure(EntityTypeBuilder<TfdConfigWhatsApp> builder)
    {
        builder.ToTable("tfd_config_whatsapp");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.BaseUrl).HasColumnName("base_url").HasMaxLength(200).IsRequired();
        builder.Property(c => c.TokenCifrado).HasColumnName("token_cifrado");
        builder.Property(c => c.PhoneNumberId).HasColumnName("phone_number_id").HasMaxLength(60);
        builder.Property(c => c.WabaId).HasColumnName("waba_id").HasMaxLength(60);
        builder.Property(c => c.VerifyTokenCifrado).HasColumnName("verify_token_cifrado");
        builder.Property(c => c.AppSecretCifrado).HasColumnName("app_secret_cifrado");
        builder.Property(c => c.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
    }
}
