using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.FaturamentoProducao;

namespace SMSMais.Data.Configurations;

internal sealed class FaturamentoProducaoUnidadeConfiguration : IEntityTypeConfiguration<FaturamentoProducaoUnidade>
{
    public void Configure(EntityTypeBuilder<FaturamentoProducaoUnidade> builder)
    {
        builder.ToTable("faturamento_producao_unidade");
        builder.HasKey(c => c.UnidadeId);

        builder.Property(c => c.UnidadeId).HasColumnName("unidade_id");
        builder.Property(c => c.Habilitada).HasColumnName("habilitada").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");

        builder.HasOne(c => c.Unidade).WithMany().HasForeignKey(c => c.UnidadeId).OnDelete(DeleteBehavior.Cascade);
    }
}
