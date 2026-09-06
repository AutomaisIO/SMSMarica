using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoSolicitacaoExigenciaConfiguration
    : IEntityTypeConfiguration<RegulacaoSolicitacaoExigencia>
{
    public void Configure(EntityTypeBuilder<RegulacaoSolicitacaoExigencia> builder)
    {
        builder.ToTable("regulacao_solicitacao_exigencia");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(x => x.RegraId).HasColumnName("regra_id");
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Obrigatoria).HasColumnName("obrigatoria").IsRequired();
        builder.Property(x => x.Situacao).HasColumnName("situacao").IsRequired();

        builder.Property(x => x.ExameInternoExameImagemId).HasColumnName("exame_interno_exame_imagem_id");
        builder.Property(x => x.ExameInternoLaudoId).HasColumnName("exame_interno_laudo_id");
        builder.Property(x => x.ValidadoExamePor).HasColumnName("validado_exame_por");
        builder.Property(x => x.ValidadoExameEm).HasColumnName("validado_exame_em");
        builder.Property(x => x.CriticaTexto).HasColumnName("critica_texto").HasMaxLength(2000);
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();

        builder.HasOne(x => x.Solicitacao).WithMany(s => s.Exigencias)
            .HasForeignKey(x => x.SolicitacaoId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SolicitacaoId).HasDatabaseName("ix_regulacao_exigencia_solicitacao");

        // Uma regra gera no máximo uma caixinha por solicitação. Parcial porque a caixinha
        // "Anexos gerais" tem regra nula e pode conviver com as outras.
        builder.HasIndex(x => new { x.SolicitacaoId, x.RegraId })
            .IsUnique()
            .HasFilter("regra_id IS NOT NULL")
            .HasDatabaseName("ux_regulacao_exigencia_regra");
    }
}

internal sealed class RegulacaoExigenciaArquivoConfiguration
    : IEntityTypeConfiguration<RegulacaoExigenciaArquivo>
{
    public void Configure(EntityTypeBuilder<RegulacaoExigenciaArquivo> builder)
    {
        builder.ToTable("regulacao_exigencia_arquivo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExigenciaId).HasColumnName("exigencia_id").IsRequired();
        builder.Property(x => x.ChaveArmazenamento)
            .HasColumnName("chave_armazenamento").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Tamanho).HasColumnName("tamanho").IsRequired();
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Versao).HasColumnName("versao").IsRequired();
        builder.Property(x => x.SubstituiArquivoId).HasColumnName("substitui_arquivo_id");
        builder.Property(x => x.Situacao).HasColumnName("situacao").IsRequired();
        builder.Property(x => x.Origem).HasColumnName("origem").IsRequired();
        builder.Property(x => x.EnviadoAoSistemaEm).HasColumnName("enviado_ao_sistema_em");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(x => x.Exigencia).WithMany(e => e.Arquivos)
            .HasForeignKey(x => x.ExigenciaId).OnDelete(DeleteBehavior.Cascade);

        // `NoAction` na auto-referência: a cadeia de substituição é histórico. Cascatear apagaria
        // a trilha inteira ao remover a primeira versão, que é justamente o que se quer guardar.
        builder.HasOne<RegulacaoExigenciaArquivo>().WithMany()
            .HasForeignKey(x => x.SubstituiArquivoId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.ExigenciaId, x.Versao })
            .HasDatabaseName("ix_regulacao_exig_arquivo");
    }
}
