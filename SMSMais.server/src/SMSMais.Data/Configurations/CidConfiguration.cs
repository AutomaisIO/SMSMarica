using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidConfiguration : IEntityTypeConfiguration<Cid>
{
    public void Configure(EntityTypeBuilder<Cid> builder)
    {
        builder.ToTable("cid");
        // Código é a chave natural — o catálogo é "um código, uma descrição".
        builder.HasKey(x => x.Codigo);
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Fonte).HasColumnName("fonte").HasMaxLength(10);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
    }
}
