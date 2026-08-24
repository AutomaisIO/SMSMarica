using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaChunkConhecimentoConfiguration : IEntityTypeConfiguration<IaChunkConhecimento>
{
    public void Configure(EntityTypeBuilder<IaChunkConhecimento> builder)
    {
        builder.ToTable("ia_chunk_conhecimento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DocumentoId).HasColumnName("documento_id").IsRequired();
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.Conteudo).HasColumnName("conteudo").IsRequired();
        builder.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("vector(1024)");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasOne(x => x.Documento).WithMany(d => d.Chunks)
            .HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.FonteId);
    }
}
