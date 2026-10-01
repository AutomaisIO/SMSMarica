using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class DocumentoPacienteConfiguration : IEntityTypeConfiguration<DocumentoPaciente>
{
    public void Configure(EntityTypeBuilder<DocumentoPaciente> builder)
    {
        builder.ToTable("documento_paciente");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(d => d.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Descricao).HasColumnName("descricao").HasMaxLength(2000);
        builder.Property(d => d.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(255).IsRequired();
        builder.Property(d => d.MimeType).HasColumnName("mime_type").HasMaxLength(120).IsRequired();
        builder.Property(d => d.TamanhoBytes).HasColumnName("tamanho_bytes").IsRequired();
        builder.Property(d => d.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();
        builder.Property(d => d.ChaveArmazenamento).HasColumnName("chave_armazenamento").HasMaxLength(400).IsRequired();
        builder.Property(d => d.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(d => d.OrigemReferencia).HasColumnName("origem_referencia").HasMaxLength(120);
        builder.Property(d => d.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(d => d.AceitoEm).HasColumnName("aceito_em");
        builder.Property(d => d.AceitoPor).HasColumnName("aceito_por");

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

        builder.HasIndex(d => new { d.PacienteId, d.CriadoEm })
            .HasDatabaseName("ix_documento_paciente_paciente_criado");

        builder.HasIndex(d => new { d.PacienteId, d.HashSha256 })
            .HasDatabaseName("ix_documento_paciente_paciente_hash");
    }
}
