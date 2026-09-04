using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregEscalaSincronizacaoExecucaoConfiguration
    : IEntityTypeConfiguration<SisregEscalaSincronizacaoExecucao>
{
    public void Configure(EntityTypeBuilder<SisregEscalaSincronizacaoExecucao> builder)
    {
        builder.ToTable("sisreg_escala_sincronizacao_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Disparo).HasColumnName("disparo").HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(x => x.EscalasLidas).HasColumnName("escalas_lidas").IsRequired();
        builder.Property(x => x.EscalasNovas).HasColumnName("escalas_novas").IsRequired();
        builder.Property(x => x.EscalasAtualizadas).HasColumnName("escalas_atualizadas").IsRequired();
        builder.Property(x => x.EscalasAusentes).HasColumnName("escalas_ausentes").IsRequired();
        builder.Property(x => x.LinhasRejeitadas).HasColumnName("linhas_rejeitadas").IsRequired();
        builder.Property(x => x.UnidadesNaoEncontradas).HasColumnName("unidades_nao_encontradas").IsRequired();
        builder.Property(x => x.Requisicoes).HasColumnName("requisicoes").IsRequired();

        builder.Property(x => x.MensagemErro).HasColumnName("mensagem_erro").HasMaxLength(2000);
        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(x => x.FinalizadoEm).HasColumnName("finalizado_em");
        builder.Property(x => x.DuracaoSegundos).HasColumnName("duracao_segundos");
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.CriadoPorNome).HasColumnName("criado_por_nome").HasMaxLength(200);

        // A tela lista "as últimas execuções" — sempre por data desc.
        builder.HasIndex(x => x.IniciadoEm).IsDescending();
    }
}
