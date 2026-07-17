using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Data.Configurations.Sisreg;

internal sealed class SisregImportacaoExecucaoConfiguration : IEntityTypeConfiguration<SisregImportacaoExecucao>
{
    public void Configure(EntityTypeBuilder<SisregImportacaoExecucao> builder)
    {
        builder.ToTable("sisreg_importacao_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LoteId).HasColumnName("lote_id").IsRequired();
        builder.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.CaminhoNoZip).HasColumnName("caminho_no_zip").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.TotalRegistros).HasColumnName("total_registros").IsRequired();
        builder.Property(x => x.Validos).HasColumnName("validos").IsRequired();
        builder.Property(x => x.Invalidos).HasColumnName("invalidos").IsRequired();
        builder.Property(x => x.JaExistiam).HasColumnName("ja_existiam").IsRequired();
        builder.Property(x => x.Mensagem).HasColumnName("mensagem").HasMaxLength(2000);
        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(x => x.ConcluidoEm).HasColumnName("concluido_em");
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.CriadoPorNome).HasColumnName("criado_por_nome").HasMaxLength(200);
        builder.Property(x => x.UnidadeExecutanteId).HasColumnName("unidade_executante_id");

        // Listagem da aba de rastreio: mais recentes primeiro, por unidade.
        builder.HasIndex(x => new { x.UnidadeExecutanteId, x.IniciadoEm })
            .HasDatabaseName("ix_sisreg_execucao_unidade_iniciado");

        // Progresso do lote em andamento.
        builder.HasIndex(x => x.LoteId).HasDatabaseName("ix_sisreg_execucao_lote");
    }
}
