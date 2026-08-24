using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Agendamentos;

namespace SMSMais.Data.Configurations.Agendamentos;

internal sealed class AgendaConfiguration : IEntityTypeConfiguration<Agenda>
{
    public void Configure(EntityTypeBuilder<Agenda> builder)
    {
        builder.ToTable("agenda", t =>
            // Recurso agendável válido por finalidade (ADR-0013):
            // Consulta exige especialidade (médico opcional = pool); Exame exige equipamento.
            t.HasCheckConstraint(
                "ck_agenda_recurso_por_finalidade",
                "(finalidade = 1 AND especialidade_id IS NOT NULL AND equipamento_id IS NULL) "
                + "OR (finalidade = 2 AND equipamento_id IS NOT NULL AND especialidade_id IS NULL AND medico_id IS NULL)"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Finalidade).HasColumnName("finalidade").HasConversion<int>().IsRequired();
        builder.Property(a => a.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(a => a.EspecialidadeId).HasColumnName("especialidade_id");
        builder.Property(a => a.MedicoId).HasColumnName("medico_id");
        builder.Property(a => a.MedicoNome).HasColumnName("medico_nome").HasMaxLength(200);
        builder.Property(a => a.MedicoCns).HasColumnName("medico_cns").HasMaxLength(15);
        builder.Property(a => a.EquipamentoId).HasColumnName("equipamento_id");
        builder.Property(a => a.DuracaoSlotMinutos).HasColumnName("duracao_slot_minutos").IsRequired();
        builder.Property(a => a.VigenciaInicio).HasColumnName("vigencia_inicio").IsRequired();
        builder.Property(a => a.VigenciaFim).HasColumnName("vigencia_fim");
        builder.Property(a => a.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(a => a.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(a => a.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(a => a.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(a => a.Unidade)
            .WithMany()
            .HasForeignKey(a => a.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Especialidade)
            .WithMany()
            .HasForeignKey(a => a.EspecialidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Equipamento)
            .WithMany()
            .HasForeignKey(a => a.EquipamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Recorrencias)
            .WithOne(r => r.Agenda)
            .HasForeignKey(r => r.AgendaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Avulsos)
            .WithOne(d => d.Agenda)
            .HasForeignKey(d => d.AgendaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Bloqueios)
            .WithOne(b => b.Agenda)
            .HasForeignKey(b => b.AgendaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Agendamentos)
            .WithOne(ag => ag.Agenda)
            .HasForeignKey(ag => ag.AgendaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.MedicoId).HasDatabaseName("ix_agenda_medico_id");
        builder.HasIndex(a => a.EquipamentoId).HasDatabaseName("ix_agenda_equipamento_id");
        builder.HasIndex(a => new { a.UnidadeId, a.EspecialidadeId }).HasDatabaseName("ix_agenda_unidade_especialidade");
        builder.HasIndex(a => a.ExcluidoEm)
            .HasDatabaseName("ix_agenda_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
