using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Pep;

namespace SMSMais.Data.Configurations.Pep;

internal sealed class PepSincronizacaoAgendaConfiguration : IEntityTypeConfiguration<PepSincronizacaoAgenda>
{
    public void Configure(EntityTypeBuilder<PepSincronizacaoAgenda> builder)
    {
        builder.ToTable("pep_sincronizacao_agenda");
        builder.HasKey(x => x.FonteId);

        builder.Property(x => x.FonteId).HasColumnName("fonte_id");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        builder.Property(x => x.IntervaloMinutos).HasColumnName("intervalo_minutos").IsRequired();
        builder.Property(x => x.JanelaInicioLocal).HasColumnName("janela_inicio_local");
        builder.Property(x => x.JanelaFimLocal).HasColumnName("janela_fim_local");
        builder.Property(x => x.MedicoRescanHoras).HasColumnName("medico_rescan_horas").IsRequired();
        builder.Property(x => x.FalhasConsecutivas).HasColumnName("falhas_consecutivas").IsRequired();
        builder.Property(x => x.ProximoRunEm).HasColumnName("proximo_run_em");
        builder.Property(x => x.PausadoAte).HasColumnName("pausado_ate");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
    }
}
