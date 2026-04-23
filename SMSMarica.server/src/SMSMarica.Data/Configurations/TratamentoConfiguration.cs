using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class TratamentoConfiguration : IEntityTypeConfiguration<Tratamento>
{
    public void Configure(EntityTypeBuilder<Tratamento> builder)
    {
        builder.ToTable("tratamento");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(t => t.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(t => t.Descricao).HasColumnName("descricao").HasMaxLength(500).IsRequired();
        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.EncerradoEm).HasColumnName("encerrado_em");

        builder.HasOne(t => t.Paciente)
            .WithMany()
            .HasForeignKey(t => t.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Unidade)
            .WithMany()
            .HasForeignKey(t => t.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Periodicidade)
            .WithOne(p => p.Tratamento!)
            .HasForeignKey<Periodicidade>(p => p.TratamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Sessoes)
            .WithOne(s => s.Tratamento)
            .HasForeignKey(s => s.TratamentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
