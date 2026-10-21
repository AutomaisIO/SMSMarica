using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class AvisoRegulacaoConfiguracaoConfiguration : IEntityTypeConfiguration<AvisoRegulacaoConfiguracao>
{
    public void Configure(EntityTypeBuilder<AvisoRegulacaoConfiguracao> builder)
    {
        builder.ToTable("aviso_regulacao_configuracao", t =>
            t.HasCheckConstraint("ck_aviso_regulacao_configuracao_dias", "lembrete_dias_antes BETWEEN 1 AND 30"));
        builder.HasKey(x => x.Sistema);

        builder.Property(x => x.Sistema).HasColumnName("sistema").HasConversion<int>().ValueGeneratedNever();
        builder.Property(x => x.LembreteHabilitado).HasColumnName("lembrete_habilitado").IsRequired();
        builder.Property(x => x.LembreteDiasAntes).HasColumnName("lembrete_dias_antes").IsRequired();
        builder.Property(x => x.AvisoCancelamentoHabilitado).HasColumnName("aviso_cancelamento_habilitado").IsRequired();
        builder.Property(x => x.AvisoCancelamentoLigadoEm).HasColumnName("aviso_cancelamento_ligado_em");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
