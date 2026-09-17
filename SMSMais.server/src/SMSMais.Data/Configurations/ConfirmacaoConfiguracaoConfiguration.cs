using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class ConfirmacaoConfiguracaoConfiguration : IEntityTypeConfiguration<ConfirmacaoConfiguracao>
{
    public void Configure(EntityTypeBuilder<ConfirmacaoConfiguracao> builder)
    {
        builder.ToTable("confirmacao_configuracao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.HoraInicioEnvio).HasColumnName("hora_inicio_envio")
            .HasDefaultValue(new TimeOnly(8, 0)).IsRequired();
        builder.Property(x => x.HoraFimEnvio).HasColumnName("hora_fim_envio")
            .HasDefaultValue(new TimeOnly(18, 0)).IsRequired();
        builder.Property(x => x.MaximoPorPassagem).HasColumnName("maximo_por_passagem")
            .HasDefaultValue(100).IsRequired();
        builder.Property(x => x.SomenteSisreg).HasColumnName("somente_sisreg")
            .HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
