using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.FaturamentoProducao;

namespace SMSMais.Data.Configurations;

internal sealed class FaturamentoProfissionalCnsConfiguration : IEntityTypeConfiguration<FaturamentoProfissionalCns>
{
    public void Configure(EntityTypeBuilder<FaturamentoProfissionalCns> builder)
    {
        builder.ToTable("faturamento_profissional_cns");
        builder.HasKey(p => p.Cpf);

        builder.Property(p => p.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(p => p.Cns).HasColumnName("cns").HasMaxLength(15).IsRequired();
        builder.Property(p => p.Cbo).HasColumnName("cbo").HasMaxLength(6);
        builder.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(120);
        builder.Property(p => p.Origem).HasColumnName("origem").HasMaxLength(20);
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
    }
}
