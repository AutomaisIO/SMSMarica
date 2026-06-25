using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class DocumentoExameConfiguration : IEntityTypeConfiguration<DocumentoExame>
{
    public void Configure(EntityTypeBuilder<DocumentoExame> builder)
    {
        builder.ToTable("documento_exame");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.SolicitacaoExameId).HasColumnName("solicitacao_exame_id").IsRequired();
        builder.Property(d => d.AnexoUploadTokenId).HasColumnName("anexo_upload_token_id");

        builder.Property(d => d.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Descricao).HasColumnName("descricao").HasMaxLength(2000);
        builder.Property(d => d.MimeType).HasColumnName("mime_type").HasMaxLength(120).IsRequired();
        builder.Property(d => d.TamanhoBytes).HasColumnName("tamanho_bytes").IsRequired();
        builder.Property(d => d.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();
        builder.Property(d => d.ChaveArmazenamento).HasColumnName("chave_armazenamento").HasMaxLength(400).IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(d => d.Origem).HasColumnName("origem").HasMaxLength(40);
        builder.Property(d => d.Paginas).HasColumnName("paginas");

        builder.Property(d => d.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(d => d.CriadoPor).HasColumnName("criado_por");
        builder.Property(d => d.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(d => d.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(d => d.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(d => d.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(d => d.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(d => d.SolicitacaoExame)
            .WithMany()
            .HasForeignKey(d => d.SolicitacaoExameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.AnexoUploadToken)
            .WithMany()
            .HasForeignKey(d => d.AnexoUploadTokenId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.SolicitacaoExameId)
            .HasDatabaseName("ix_documento_exame_solicitacao_exame_id");

        builder.HasIndex(d => d.HashSha256);
    }
}
