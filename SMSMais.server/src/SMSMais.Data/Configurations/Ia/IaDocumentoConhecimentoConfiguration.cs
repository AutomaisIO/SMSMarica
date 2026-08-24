using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaDocumentoConhecimentoConfiguration : IEntityTypeConfiguration<IaDocumentoConhecimento>
{
    public void Configure(EntityTypeBuilder<IaDocumentoConhecimento> builder)
    {
        builder.ToTable("ia_documento_conhecimento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.Caminho).HasColumnName("caminho").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Conteudo).HasColumnName("conteudo").IsRequired();
        builder.Property(x => x.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Versao).HasColumnName("versao").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(x => x.Fonte).WithMany().HasForeignKey(x => x.FonteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.FonteId, x.Caminho }).IsUnique();
    }
}
