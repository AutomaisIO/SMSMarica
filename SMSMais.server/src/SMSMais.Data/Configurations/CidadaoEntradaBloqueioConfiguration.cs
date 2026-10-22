using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidadaoEntradaBloqueioConfiguration : IEntityTypeConfiguration<CidadaoEntradaBloqueio>
{
    public void Configure(EntityTypeBuilder<CidadaoEntradaBloqueio> builder)
    {
        builder.ToTable("cidadao_entrada_bloqueio");
        builder.HasKey(b => b.Cpf);

        builder.Property(b => b.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(b => b.Falhas).HasColumnName("falhas").IsRequired();
        builder.Property(b => b.PrimeiraFalhaEm).HasColumnName("primeira_falha_em").IsRequired();
        builder.Property(b => b.BloqueadoAte).HasColumnName("bloqueado_ate");
    }
}
