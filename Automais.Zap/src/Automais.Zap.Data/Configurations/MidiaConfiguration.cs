using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class MidiaConfiguration : IEntityTypeConfiguration<Midia>
{
    public void Configure(EntityTypeBuilder<Midia> b)
    {
        b.ToTable("midia");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(260).IsRequired();
        b.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100).IsRequired();
        b.Property(x => x.Conteudo).HasColumnName("conteudo").IsRequired();
        b.Property(x => x.TamanhoBytes).HasColumnName("tamanho_bytes");
        b.Property(x => x.Largura).HasColumnName("largura");
        b.Property(x => x.Altura).HasColumnName("altura");
        b.Property(x => x.HashSha256).HasColumnName("hash_sha256").HasMaxLength(64).IsRequired();
        b.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(60);
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.CriadoPorUsuarioId).HasColumnName("criado_por_usuario_id");

        // Mesmo arquivo reenviado devolve a MESMA URL: o dedup é por tenant para que a arte de
        // um município não vire silenciosamente a do outro por coincidência de bytes.
        b.HasIndex(x => new { x.TenantId, x.HashSha256 })
            .IsUnique().HasDatabaseName("ux_midia_tenant_hash");

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
