using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class MidiaConfiguration : IEntityTypeConfiguration<Midia>
{
    public void Configure(EntityTypeBuilder<Midia> builder)
    {
        builder.ToTable("midia");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(260).IsRequired();
        builder.Property(m => m.MimeType).HasColumnName("mime_type").HasMaxLength(120).IsRequired();
        builder.Property(m => m.Conteudo).HasColumnName("conteudo").HasColumnType("bytea").IsRequired();
        builder.Property(m => m.TamanhoBytes).HasColumnName("tamanho_bytes").IsRequired();
        builder.Property(m => m.Largura).HasColumnName("largura");
        builder.Property(m => m.Altura).HasColumnName("altura");
        builder.Property(m => m.Categoria).HasColumnName("categoria").HasMaxLength(80);
        builder.Property(m => m.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();

        builder.Property(m => m.CriadoPorUsuarioId).HasColumnName("criado_por_usuario_id");
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasOne(m => m.CriadoPorUsuario)
            .WithMany()
            .HasForeignKey(m => m.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.Categoria);
        builder.HasIndex(m => m.HashSha256);
    }
}
