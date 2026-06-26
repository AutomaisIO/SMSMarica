using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Integracoes;

namespace SMSMarica.Data.Configurations.Integracoes;

internal sealed class ProxyMotorConfigConfiguration : IEntityTypeConfiguration<ProxyMotorConfig>
{
    public void Configure(EntityTypeBuilder<ProxyMotorConfig> builder)
    {
        builder.ToTable("proxy_motor");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Servico).HasColumnName("servico").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Motor).HasColumnName("motor").HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.Servico, x.Motor }).IsUnique();

        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.TokenCifrado).HasColumnName("token_cifrado");
        builder.Property(x => x.TimeoutSegundos).HasColumnName("timeout_segundos").IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.ParametrosJson).HasColumnName("parametros_json").HasColumnType("jsonb");

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
