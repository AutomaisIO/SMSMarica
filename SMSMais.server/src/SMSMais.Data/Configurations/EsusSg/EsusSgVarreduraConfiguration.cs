using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Data.Configurations.EsusSg;

internal sealed class EsusSgVarreduraExecucaoConfiguration : IEntityTypeConfiguration<EsusSgVarreduraExecucao>
{
    public void Configure(EntityTypeBuilder<EsusSgVarreduraExecucao> builder)
    {
        builder.ToTable("esussg_varredura_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Modo).HasColumnName("modo").IsRequired()
            .HasDefaultValue(ModoVarreduraEsusSg.Diaria);
        builder.Property(x => x.Disparo).HasColumnName("disparo").IsRequired()
            .HasDefaultValue(DisparoSincronizacao.Manual);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.JanelaInicio).HasColumnName("janela_inicio").IsRequired();
        builder.Property(x => x.JanelaFim).HasColumnName("janela_fim").IsRequired();

        builder.Property(x => x.Requisicoes).HasColumnName("requisicoes").IsRequired();
        builder.Property(x => x.NaFila).HasColumnName("na_fila").IsRequired();
        builder.Property(x => x.AgendadosLidos).HasColumnName("agendados_lidos").IsRequired();
        builder.Property(x => x.SolicitacoesNovas).HasColumnName("solicitacoes_novas").IsRequired();
        builder.Property(x => x.SolicitacoesAtualizadas).HasColumnName("solicitacoes_atualizadas").IsRequired();
        builder.Property(x => x.MudancasSituacao).HasColumnName("mudancas_situacao").IsRequired();
        builder.Property(x => x.SaidasDaFila).HasColumnName("saidas_da_fila").IsRequired();
        builder.Property(x => x.EventosNovos).HasColumnName("eventos_novos").IsRequired();
        builder.Property(x => x.GatilhosGerados).HasColumnName("gatilhos_gerados").IsRequired();
        builder.Property(x => x.MesesIncompletos).HasColumnName("meses_incompletos").IsRequired();

        builder.Property(x => x.Fase).HasColumnName("fase").IsRequired()
            .HasDefaultValue(FaseVarreduraEsusSg.Fila);
        builder.Property(x => x.CursorMes).HasColumnName("cursor_mes");
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
            .HasDatabaseName("ix_esussg_varredura_execucao_iniciado")
            .IsDescending(true);
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_esussg_varredura_execucao_status");
    }
}

internal sealed class EsusSgVarreduraFalhaConfiguration : IEntityTypeConfiguration<EsusSgVarreduraFalha>
{
    public void Configure(EntityTypeBuilder<EsusSgVarreduraFalha> builder)
    {
        builder.ToTable("esussg_varredura_falha");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.TipoRecurso).HasColumnName("tipo_recurso");
        builder.Property(x => x.Mes).HasColumnName("mes");
        builder.Property(x => x.Mensagem).HasColumnName("mensagem").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Detalhe).HasColumnName("detalhe");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(x => new { x.ExecucaoId, x.Tipo })
            .HasDatabaseName("ix_esussg_varredura_falha_execucao_tipo");

        builder.HasOne(x => x.Execucao)
            .WithMany()
            .HasForeignKey(x => x.ExecucaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EsusSgCatalogoRecursoConfiguration : IEntityTypeConfiguration<EsusSgCatalogoRecurso>
{
    public void Configure(EntityTypeBuilder<EsusSgCatalogoRecurso> builder)
    {
        builder.ToTable("esussg_catalogo_recurso");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();

        builder.Property(x => x.RotuloChave).HasColumnName("rotulo_chave").HasMaxLength(300);

        // Identidade = NOME (07/10/2026). O `value` do combo é posição — a SES renumera o combo
        // inteiro quando acrescenta um recurso — e por isso deixou de ser único: dois recursos
        // podem ter tido o mesmo número em dias diferentes. Parcial porque linha antiga nasce sem
        // a chave e só a ganha quando a cópia consolida.
        builder.HasIndex(x => new { x.Tipo, x.RotuloChave })
            .IsUnique().HasFilter("rotulo_chave IS NOT NULL")
            .HasDatabaseName("ux_esussg_catalogo_recurso_nome");
        builder.HasIndex(x => new { x.Tipo, x.Valor })
            .HasDatabaseName("ix_esussg_catalogo_recurso_valor");
    }
}
