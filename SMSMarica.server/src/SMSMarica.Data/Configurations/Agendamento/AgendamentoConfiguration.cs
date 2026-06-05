using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Agendamentos;

namespace SMSMarica.Data.Configurations.Agendamentos;

internal sealed class AgendamentoConfiguration : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> builder)
    {
        builder.ToTable("agendamento");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.AgendaId).HasColumnName("agenda_id").IsRequired();
        builder.Property(a => a.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(a => a.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200).IsRequired();
        builder.Property(a => a.PacienteCns).HasColumnName("paciente_cns").HasMaxLength(15);
        builder.Property(a => a.InicioEm).HasColumnName("inicio_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(a => a.FimEm).HasColumnName("fim_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(a => a.Observacao).HasColumnName("observacao").HasMaxLength(1000);
        builder.Property(a => a.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(a => a.RealizadoEm).HasColumnName("realizado_em");
        builder.Property(a => a.CanceladoEm).HasColumnName("cancelado_em");
        builder.Property(a => a.MotivoCancelamento).HasColumnName("motivo_cancelamento").HasMaxLength(300);

        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(a => a.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(a => a.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(a => a.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasIndex(a => new { a.AgendaId, a.InicioEm }).HasDatabaseName("ix_agendamento_agenda_inicio");
        builder.HasIndex(a => a.PacienteId).HasDatabaseName("ix_agendamento_paciente_id");
        builder.HasIndex(a => a.ExcluidoEm)
            .HasDatabaseName("ix_agendamento_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
