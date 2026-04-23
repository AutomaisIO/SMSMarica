using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class MotoristaConfiguration : IEntityTypeConfiguration<Motorista>
{
    public void Configure(EntityTypeBuilder<Motorista> builder)
    {
        builder.ToTable("motorista");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.NomeCompleto).HasColumnName("nome_completo").HasMaxLength(200).IsRequired();
        builder.Property(m => m.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(m => m.Cnh).HasColumnName("cnh").HasMaxLength(11).IsRequired();
        builder.Property(m => m.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(m => m.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(m => m.Cpf).IsUnique();
        builder.HasIndex(m => m.Cnh).IsUnique();
    }
}
