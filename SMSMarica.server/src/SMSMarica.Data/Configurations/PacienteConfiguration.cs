using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class PacienteConfiguration : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("paciente");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.NomeCompleto).HasColumnName("nome_completo").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(p => p.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(p => p.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em");

        builder.OwnsOne(p => p.GpsResidencia, gps =>
        {
            gps.Property(g => g.Latitude).HasColumnName("residencia_latitude").IsRequired();
            gps.Property(g => g.Longitude).HasColumnName("residencia_longitude").IsRequired();
        });

        builder.HasIndex(p => p.Cpf).IsUnique();
    }
}
