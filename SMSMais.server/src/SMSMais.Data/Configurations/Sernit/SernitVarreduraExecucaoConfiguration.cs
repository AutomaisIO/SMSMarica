using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Data.Configurations.Sernit;

internal sealed class SernitVarreduraExecucaoConfiguration : IEntityTypeConfiguration<SernitVarreduraExecucao>
{
    public void Configure(EntityTypeBuilder<SernitVarreduraExecucao> builder)
    {
        builder.ToTable("sernit_varredura_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Modo).HasColumnName("modo").IsRequired()
            .HasDefaultValue(ModoVarreduraSernit.Diaria);
        builder.Property(x => x.Disparo).HasColumnName("disparo").IsRequired()
            .HasDefaultValue(DisparoSincronizacao.Manual);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.JanelaInicio).HasColumnName("janela_inicio").IsRequired();
        builder.Property(x => x.JanelaFim).HasColumnName("janela_fim").IsRequired();
        builder.Property(x => x.SituacoesVarridas).HasColumnName("situacoes_varridas").HasMaxLength(300).IsRequired();

        builder.Property(x => x.Buscas).HasColumnName("buscas").IsRequired();
        builder.Property(x => x.Paginas).HasColumnName("paginas").IsRequired();
        builder.Property(x => x.SolicitacoesEncontradas).HasColumnName("solicitacoes_encontradas").IsRequired();
        builder.Property(x => x.SolicitacoesNovas).HasColumnName("solicitacoes_novas").IsRequired();
        builder.Property(x => x.SolicitacoesAtualizadas).HasColumnName("solicitacoes_atualizadas").IsRequired();
        builder.Property(x => x.MudancasSituacao).HasColumnName("mudancas_situacao").IsRequired();

        builder.Property(x => x.HistoricosLidos).HasColumnName("historicos_lidos").IsRequired();
        builder.Property(x => x.EventosNovos).HasColumnName("eventos_novos").IsRequired();
        builder.Property(x => x.FollowUpsNovos).HasColumnName("followups_novos").IsRequired();
        builder.Property(x => x.HistoricosIndisponiveis).HasColumnName("historicos_indisponiveis").IsRequired();
        builder.Property(x => x.GatilhosGerados).HasColumnName("gatilhos_gerados").IsRequired();
        builder.Property(x => x.FatiasTruncadas).HasColumnName("fatias_truncadas").IsRequired();

        builder.Property(x => x.Fase).HasColumnName("fase").IsRequired()
            .HasDefaultValue(FaseVarreduraSernit.Grade);
        builder.Property(x => x.CursorSituacao).HasColumnName("cursor_situacao");
        builder.Property(x => x.CursorData).HasColumnName("cursor_data");
        builder.Property(x => x.CursorIdSernit).HasColumnName("cursor_id_sernit").HasMaxLength(20);
        builder.Property(x => x.HistoricosPendentes).HasColumnName("historicos_pendentes")
            .IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Retomadas).HasColumnName("retomadas").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.RetomadaEm).HasColumnName("retomada_em");
        builder.Property(x => x.UltimoSinalEm).HasColumnName("ultimo_sinal_em");

        builder.Property(x => x.MensagemErro).HasColumnName("mensagem_erro").HasMaxLength(2000);
        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(x => x.FinalizadoEm).HasColumnName("finalizado_em");
        builder.Property(x => x.DuracaoSegundos).HasColumnName("duracao_segundos");
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.CriadoPorNome).HasColumnName("criado_por_nome").HasMaxLength(200);

        builder.HasIndex(x => x.IniciadoEm)
            .HasDatabaseName("ix_sernit_varredura_execucao_iniciado")
            .IsDescending(true);

        builder.HasIndex(x => x.Status).HasDatabaseName("ix_sernit_varredura_execucao_status");
    }
}
