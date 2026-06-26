using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class NumeroValidadoConfiguration : IEntityTypeConfiguration<NumeroValidado>
{
    public void Configure(EntityTypeBuilder<NumeroValidado> builder)
    {
        builder.ToTable("numero_validado");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.Numero).HasColumnName("numero").HasMaxLength(20).IsRequired();
        builder.Property(n => n.ValidadoEm).HasColumnName("validado_em").IsRequired();
        builder.Property(n => n.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
        builder.Property(n => n.ValidadoPor).HasColumnName("validado_por");

        // 1 registro por número (o número é a chave de negócio).
        builder.HasIndex(n => n.Numero).IsUnique().HasDatabaseName("ux_numero_validado_numero");
    }
}
