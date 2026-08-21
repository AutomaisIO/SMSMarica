using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class DestinoConfiguration : IEntityTypeConfiguration<Destino>
{
    public void Configure(EntityTypeBuilder<Destino> b)
    {
        b.ToTable("destino");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.UrlWebhook).HasColumnName("url_webhook").HasMaxLength(500).IsRequired();
        b.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);
        b.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(500);
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");

        b.HasIndex(x => x.Nome).IsUnique().HasDatabaseName("ux_destino_nome");
    }
}
