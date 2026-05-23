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
        builder.Property(m => m.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(m => m.Cnh).HasColumnName("cnh").HasMaxLength(11).IsRequired();
        builder.Property(m => m.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(m => m.Usuario)
            .WithOne()
            .HasForeignKey<Motorista>(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.UsuarioId).IsUnique();
        builder.HasIndex(m => m.Cnh).IsUnique();
    }
}
