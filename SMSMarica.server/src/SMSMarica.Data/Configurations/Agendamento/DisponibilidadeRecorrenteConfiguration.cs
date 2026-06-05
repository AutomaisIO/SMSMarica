using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Agendamentos;

namespace SMSMarica.Data.Configurations.Agendamentos;

internal sealed class DisponibilidadeRecorrenteConfiguration : IEntityTypeConfiguration<DisponibilidadeRecorrente>
{
    public void Configure(EntityTypeBuilder<DisponibilidadeRecorrente> builder)
    {
        builder.ToTable("disponibilidade_recorrente");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.AgendaId).HasColumnName("agenda_id").IsRequired();
        builder.Property(r => r.DiaSemana).HasColumnName("dia_semana").HasConversion<int>().IsRequired();
        builder.Property(r => r.HoraInicio).HasColumnName("hora_inicio").IsRequired();
        builder.Property(r => r.HoraFim).HasColumnName("hora_fim").IsRequired();
        builder.Property(r => r.VigenciaInicio).HasColumnName("vigencia_inicio");
        builder.Property(r => r.VigenciaFim).HasColumnName("vigencia_fim");
        builder.Property(r => r.Ativo).HasColumnName("ativo").IsRequired();
        builder.Property(r => r.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(r => r.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(r => r.AgendaId).HasDatabaseName("ix_disponibilidade_recorrente_agenda_id");
    }
}
