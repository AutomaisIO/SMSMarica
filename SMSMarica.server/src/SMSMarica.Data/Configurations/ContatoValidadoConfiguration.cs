using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class ContatoValidadoConfiguration : IEntityTypeConfiguration<ContatoValidado>
{
    public void Configure(EntityTypeBuilder<ContatoValidado> builder)
    {
        builder.ToTable("contato_validado");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(c => c.Numero).HasColumnName("numero").HasMaxLength(20).IsRequired();
        builder.Property(c => c.ValidadoEm).HasColumnName("validado_em").IsRequired();
        builder.Property(c => c.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
        builder.Property(c => c.ValidadoPor).HasColumnName("validado_por");

        // 1 contato principal por pessoa (CPF) E 1 dono por número (impede duplicidade entre pessoas).
        builder.HasIndex(c => c.Cpf).IsUnique().HasDatabaseName("ux_contato_validado_cpf");
        builder.HasIndex(c => c.Numero).IsUnique().HasDatabaseName("ux_contato_validado_numero");
    }
}
