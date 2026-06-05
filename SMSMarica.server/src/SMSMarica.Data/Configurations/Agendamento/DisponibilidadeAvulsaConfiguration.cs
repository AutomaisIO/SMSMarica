using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Agendamentos;

namespace SMSMarica.Data.Configurations.Agendamentos;

internal sealed class DisponibilidadeAvulsaConfiguration : IEntityTypeConfiguration<DisponibilidadeAvulsa>
{
    public void Configure(EntityTypeBuilder<DisponibilidadeAvulsa> builder)
    {
        builder.ToTable("disponibilidade_avulsa");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.AgendaId).HasColumnName("agenda_id").IsRequired();
        // Horário local (wall-clock): timestamp sem fuso para o cálculo de slots não depender de timezone.
        builder.Property(d => d.InicioEm).HasColumnName("inicio_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(d => d.FimEm).HasColumnName("fim_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(d => d.Motivo).HasColumnName("motivo").HasMaxLength(300);
        builder.Property(d => d.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(d => d.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(d => d.AgendaId).HasDatabaseName("ix_disponibilidade_avulsa_agenda_id");
    }
}
