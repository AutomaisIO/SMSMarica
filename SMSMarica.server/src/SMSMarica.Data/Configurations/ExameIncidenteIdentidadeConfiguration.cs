using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Configurations;

internal sealed class ExameIncidenteIdentidadeConfiguration : IEntityTypeConfiguration<ExameIncidenteIdentidade>
{
    public void Configure(EntityTypeBuilder<ExameIncidenteIdentidade> builder)
    {
        builder.ToTable("exame_incidente_identidade");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.StudyInstanceUID)
            .HasColumnName("study_instance_uid").HasMaxLength(128).IsRequired();
        // Sem FK: o incidente é histórico e sobrevive à exclusão do exame.
        builder.Property(i => i.ExameImagemId).HasColumnName("exame_imagem_id");
        builder.Property(i => i.PacienteSuspeitoId).HasColumnName("paciente_suspeito_id");
        builder.Property(i => i.Motivo).HasColumnName("motivo").IsRequired();
        builder.Property(i => i.Status)
            .HasColumnName("status").HasDefaultValue(StatusIncidenteIdentidade.Aberto).IsRequired();
        builder.Property(i => i.Automatico)
            .HasColumnName("automatico").HasDefaultValue(false).IsRequired();
        builder.Property(i => i.ResolvidoEm).HasColumnName("resolvido_em");
        builder.Property(i => i.ResolvidoPor).HasColumnName("resolvido_por");
        builder.Property(i => i.ResolucaoNota).HasColumnName("resolucao_nota");
        builder.Property(i => i.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(i => i.CriadoPor).HasColumnName("criado_por");
        builder.Property(i => i.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(i => i.AtualizadoPor).HasColumnName("atualizado_por");

        // UM incidente aberto por estudo: o segundo alarme sobre o mesmo estudo não vira fila
        // duplicada. Filtrado, para que o histórico de resolvidos/descartados fique.
        builder.HasIndex(i => i.StudyInstanceUID)
            .HasDatabaseName("ix_exame_incidente_identidade_study_aberto")
            .IsUnique()
            .HasFilter($"status = {(int)StatusIncidenteIdentidade.Aberto}");

        builder.HasIndex(i => i.Status);
    }
}
