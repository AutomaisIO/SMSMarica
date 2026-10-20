using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregVarreduraAgendaConfiguration : IEntityTypeConfiguration<SisregVarreduraAgenda>
{
    public void Configure(EntityTypeBuilder<SisregVarreduraAgenda> builder)
    {
        builder.ToTable("sisreg_varredura_agenda");

        // A unidade É a chave: uma agenda por unidade, sem id sintético para desincronizar.
        builder.HasKey(x => x.UnidadeId);

        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired().HasDefaultValue(false);
        // "Nasce LIGADO" desde 20/10/2026 (reverte o opt-in de 03/08/2026): os pontos de criação da
        // varredura setam EnviarConfirmacao = true EXPLICITAMENTE. O default do BANCO fica em false
        // DE PROPÓSITO (sentinela do EF para bool): o true explícito da unidade nova é gravado e o
        // opt-out (gravar false para desligar a unidade) continua funcionando. NÃO trocar para
        // HasDefaultValue(true): quebraria o opt-out.
        builder.Property(x => x.EnviarConfirmacao).HasColumnName("enviar_confirmacao")
            .IsRequired().HasDefaultValue(false);
        // Default FALSE: muda o comportamento de um motor de produção, a unidade escolhe adotar.
        builder.Property(x => x.HoraLocal).HasColumnName("hora_local").IsRequired();
        builder.Property(x => x.DiasAFrente).HasColumnName("dias_a_frente").IsRequired().HasDefaultValue(21);
        builder.Property(x => x.FalhasConsecutivas).HasColumnName("falhas_consecutivas").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ProximoRunEm).HasColumnName("proximo_run_em");
        builder.Property(x => x.PausadoAte).HasColumnName("pausado_ate");
        builder.Property(x => x.UltimaExecucaoEm).HasColumnName("ultima_execucao_em");
        builder.Property(x => x.UltimaExecucaoId).HasColumnName("ultima_execucao_id");
        builder.Property(x => x.HistoricoAtivo).HasColumnName("historico_ativo").IsRequired();
        builder.Property(x => x.HistoricoCobertoDe).HasColumnName("historico_coberto_de");
        builder.Property(x => x.HistoricoFatiasVazias)
            .HasColumnName("historico_fatias_vazias").IsRequired();
        builder.Property(x => x.HistoricoConcluidoEm).HasColumnName("historico_concluido_em");

        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        // O scheduler varre por elegibilidade: só as ativas interessam.
        builder.HasIndex(x => new { x.Ativo, x.ProximoRunEm })
            .HasDatabaseName("ix_sisreg_varredura_agenda_elegivel");

        builder.HasOne<Entities.Unidade>()
            .WithMany()
            .HasForeignKey(x => x.UnidadeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
