using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class RoboAtendimentoTarefaConfiguration : IEntityTypeConfiguration<RoboAtendimentoTarefa>
{
    public void Configure(EntityTypeBuilder<RoboAtendimentoTarefa> builder)
    {
        builder.ToTable("robo_tarefa");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.ConversaId).HasColumnName("conversa_id").IsRequired();
        builder.Property(t => t.MensagemWhatsAppId).HasColumnName("mensagem_whatsapp_id").IsRequired();
        builder.Property(t => t.PacienteId).HasColumnName("paciente_id");
        builder.Property(t => t.RoboAssuntoId).HasColumnName("robo_assunto_id");
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(t => t.SessionIdAiengine).HasColumnName("session_id_aiengine").HasMaxLength(200);
        builder.Property(t => t.ConfiancaUltima).HasColumnName("confianca_ultima");
        builder.Property(t => t.Tentativas).HasColumnName("tentativas").HasDefaultValue(0).IsRequired();
        builder.Property(t => t.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");
        builder.Property(t => t.Erro).HasColumnName("erro");
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(t => t.Conversa)
            .WithMany()
            .HasForeignKey(t => t.ConversaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.RoboAssunto)
            .WithMany()
            .HasForeignKey(t => t.RoboAssuntoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Mensagem)
            .WithMany()
            .HasForeignKey(t => t.MensagemWhatsAppId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma tarefa por mensagem inbound — idempotência do enfileiramento.
        builder.HasIndex(t => t.MensagemWhatsAppId)
            .HasDatabaseName("ux_robo_tarefa_mensagem")
            .IsUnique();

        // Varredura do worker: Pendentes por ProximaTentativaEm.
        builder.HasIndex(t => new { t.Status, t.ProximaTentativaEm })
            .HasDatabaseName("ix_robo_tarefa_status_proxima");
    }
}
