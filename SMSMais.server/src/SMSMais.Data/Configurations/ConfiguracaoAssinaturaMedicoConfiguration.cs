using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class ConfiguracaoAssinaturaMedicoConfiguration : IEntityTypeConfiguration<ConfiguracaoAssinaturaMedico>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoAssinaturaMedico> builder)
    {
        builder.ToTable("medico_config_assinatura");
        builder.HasKey(c => c.MedicoId);

        builder.Property(c => c.MedicoId).HasColumnName("medico_id").ValueGeneratedNever();
        builder.Property(c => c.Modo).HasColumnName("modo").HasConversion<int>().IsRequired();

        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.CriadoPor).HasColumnName("criado_por");
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
