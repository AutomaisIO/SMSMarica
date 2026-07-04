using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class AgendamentoNotificacaoConfiguration : IEntityTypeConfiguration<AgendamentoNotificacao>
{
    public void Configure(EntityTypeBuilder<AgendamentoNotificacao> builder)
    {
        builder.ToTable("agendamento_notificacao");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(n => n.SolicitacaoExameId).HasColumnName("solicitacao_exame_id");
        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.Property(n => n.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(n => n.Telefone).HasColumnName("telefone").HasMaxLength(20);
        builder.Property(n => n.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(n => n.MotivoFalha).HasColumnName("motivo_falha").HasMaxLength(1000);
        builder.Property(n => n.LoginLinkId).HasColumnName("login_link_id");
        builder.Property(n => n.MensagemWhatsAppId).HasColumnName("mensagem_whatsapp_id");
        builder.Property(n => n.Tentativas).HasColumnName("tentativas").HasDefaultValue(0).IsRequired();
        builder.Property(n => n.UltimaTentativaEm).HasColumnName("ultima_tentativa_em");
        builder.Property(n => n.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");
        builder.Property(n => n.EnviadoEm).HasColumnName("enviado_em");
        builder.Property(n => n.EntregueEm).HasColumnName("entregue_em");
        builder.Property(n => n.LidoEm).HasColumnName("lido_em");
        builder.Property(n => n.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(n => n.AtualizadoEm).HasColumnName("atualizado_em");

        builder.Property(n => n.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(n => n.SolicitacaoExame)
            .WithMany()
            .HasForeignKey(n => n.SolicitacaoExameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.LoginLink)
            .WithMany()
            .HasForeignKey(n => n.LoginLinkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(n => n.MensagemWhatsApp)
            .WithMany()
            .HasForeignKey(n => n.MensagemWhatsAppId)
            .OnDelete(DeleteBehavior.SetNull);

        // Fila do worker.
        builder.HasIndex(n => new { n.Status, n.ProximaTentativaEm });
        // Uma notificação por solicitação de exame.
        builder.HasIndex(n => n.SolicitacaoExameId)
            .IsUnique()
            .HasFilter("solicitacao_exame_id IS NOT NULL");
        builder.HasIndex(n => n.MensagemWhatsAppId);
        builder.HasIndex(n => n.PacienteId);
    }
}
