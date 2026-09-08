using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregVarreduraExecucaoConfiguration : IEntityTypeConfiguration<SisregVarreduraExecucao>
{
    public void Configure(EntityTypeBuilder<SisregVarreduraExecucao> builder)
    {
        builder.ToTable("sisreg_varredura_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.UnidadeNome).HasColumnName("unidade_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Disparo).HasColumnName("disparo").IsRequired()
            .HasDefaultValue(DisparoSincronizacao.Manual);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.JanelaInicio).HasColumnName("janela_inicio").IsRequired();
        builder.Property(x => x.JanelaFim).HasColumnName("janela_fim").IsRequired();
        builder.Property(x => x.CombinacoesTotal).HasColumnName("combinacoes_total").IsRequired();
        builder.Property(x => x.CombinacoesFeitas).HasColumnName("combinacoes_feitas").IsRequired();
        builder.Property(x => x.Requisicoes).HasColumnName("requisicoes").IsRequired();
        builder.Property(x => x.RegistrosEncontrados).HasColumnName("registros_encontrados").IsRequired();
        builder.Property(x => x.Validos).HasColumnName("validos").IsRequired();
        builder.Property(x => x.Invalidos).HasColumnName("invalidos").IsRequired();
        builder.Property(x => x.JaExistiam).HasColumnName("ja_existiam").IsRequired();
        builder.Property(x => x.MensagemErro).HasColumnName("mensagem_erro").HasMaxLength(2000);
        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(x => x.FinalizadoEm).HasColumnName("finalizado_em");
        builder.Property(x => x.DuracaoSegundos).HasColumnName("duracao_segundos");
        builder.Property(x => x.UltimoSinalEm).HasColumnName("ultimo_sinal_em");
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.CriadoPorNome).HasColumnName("criado_por_nome").HasMaxLength(200);

        // Histórico por unidade, mais recente primeiro — é como a tela lista.
        builder.HasIndex(x => new { x.UnidadeId, x.IniciadoEm })
            .HasDatabaseName("ix_sisreg_varredura_execucao_unidade_iniciado")
            .IsDescending(false, true);

        // Sem FK para unidade: o rastreio tem que sobreviver à exclusão do cadastro
        // (o nome fica desnormalizado justamente para isso).
    }
}
