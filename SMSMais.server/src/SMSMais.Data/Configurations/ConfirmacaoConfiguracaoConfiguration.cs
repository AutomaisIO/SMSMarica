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
        builder.Property(x => x.LembreteDiasAntes).HasColumnName("lembrete_dias_antes")
            .HasDefaultValue(2).IsRequired();
        builder.Property(x => x.LembreteHabilitado).HasColumnName("lembrete_habilitado")
            .HasDefaultValue(false).IsRequired();
        builder.Property(x => x.ConciliacaoCancelamentoHabilitada)
            .HasColumnName("conciliacao_cancelamento_habilitada").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.AvisoCancelamentoHabilitado)
            .HasColumnName("aviso_cancelamento_habilitado").HasDefaultValue(false).IsRequired();
        // ValueGeneratedNever nos quatro: o default de banco serve para a coluna NASCER povoada nas
        // linhas que já existiam, e só. Sem isso o EF trata a coluna como gerada e OMITE do INSERT
        // todo valor igual ao default do CLR — hora 0 sumiria do comando e o Postgres gravaria 8,
        // calado, numa instância nova (a linha é singleton e só nasce no primeiro salvamento).
        builder.Property(x => x.ConciliacaoIntervaloMinutos)
            .HasColumnName("conciliacao_intervalo_minutos").HasDefaultValue(10).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.ConciliacaoHoraInicio)
            .HasColumnName("conciliacao_hora_inicio").HasDefaultValue(8).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.ConciliacaoHoraFim)
            .HasColumnName("conciliacao_hora_fim").HasDefaultValue(18).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.ConciliacaoHoraFechamento)
            .HasColumnName("conciliacao_hora_fechamento").HasDefaultValue(7).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.ConciliacaoUltimoDiaFechado)
            .HasColumnName("conciliacao_ultimo_dia_fechado");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
