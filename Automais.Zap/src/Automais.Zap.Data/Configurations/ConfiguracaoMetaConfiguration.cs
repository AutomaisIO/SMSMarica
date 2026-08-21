using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class ConfiguracaoMetaConfiguration : IEntityTypeConfiguration<ConfiguracaoMeta>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoMeta> b)
    {
        b.ToTable("configuracao_meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.AppId).HasColumnName("app_id").HasMaxLength(60);
        b.Property(x => x.AppSecretCifrado).HasColumnName("app_secret_cifrado").HasMaxLength(1000);
        b.Property(x => x.VerifyTokenCifrado).HasColumnName("verify_token_cifrado").HasMaxLength(1000);
        b.Property(x => x.TokenSistemaCifrado).HasColumnName("token_sistema_cifrado").HasMaxLength(4000);
        b.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(200).IsRequired();
        b.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
    }
}
