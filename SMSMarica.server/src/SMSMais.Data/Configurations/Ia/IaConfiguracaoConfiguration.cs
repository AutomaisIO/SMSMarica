using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaConfiguracaoConfiguration : IEntityTypeConfiguration<IaConfiguracao>
{
    public void Configure(EntityTypeBuilder<IaConfiguracao> builder)
    {
        builder.ToTable("ia_configuracao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Provedor).HasColumnName("provedor").HasMaxLength(50).IsRequired();
        builder.Property(x => x.TokenCifrado).HasColumnName("token_cifrado");
        builder.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProvedorEmbeddings).HasColumnName("provedor_embeddings").HasMaxLength(50).IsRequired();
        builder.Property(x => x.TokenEmbeddingsCifrado).HasColumnName("token_embeddings_cifrado");
        builder.Property(x => x.ModeloEmbeddings).HasColumnName("modelo_embeddings").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
