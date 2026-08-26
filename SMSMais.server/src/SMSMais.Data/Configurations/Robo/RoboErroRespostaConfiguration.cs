using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class RoboErroRespostaConfiguration : IEntityTypeConfiguration<RoboErroResposta>
{
    public void Configure(EntityTypeBuilder<RoboErroResposta> builder)
    {
        builder.ToTable("robo_erro_resposta");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ConversaId).HasColumnName("conversa_id").IsRequired();
        builder.Property(e => e.MensagemWhatsAppId).HasColumnName("mensagem_whatsapp_id");
        builder.Property(e => e.RoboAssuntoId).HasColumnName("robo_assunto_id");
        builder.Property(e => e.Trecho).HasColumnName("trecho").HasColumnType("text");
        builder.Property(e => e.Nota).HasColumnName("nota").HasMaxLength(2000);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por");
        builder.Property(e => e.RevisadoEm).HasColumnName("revisado_em");
        builder.Property(e => e.RevisadoPor).HasColumnName("revisado_por");
        builder.Property(e => e.RevisaoNota).HasColumnName("revisao_nota").HasMaxLength(2000);

        builder.HasOne(e => e.Conversa).WithMany().HasForeignKey(e => e.ConversaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.RoboAssunto).WithMany().HasForeignKey(e => e.RoboAssuntoId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Mensagem).WithMany().HasForeignKey(e => e.MensagemWhatsAppId)
            .OnDelete(DeleteBehavior.SetNull);

        // Um erro ABERTO por mensagem (idempotência da marcação); reabrir/atualizar em vez de duplicar.
        builder.HasIndex(e => e.MensagemWhatsAppId)
            .HasDatabaseName("ux_robo_erro_mensagem_aberto")
            .IsUnique()
            .HasFilter("mensagem_whatsapp_id IS NOT NULL AND status = 1");

        builder.HasIndex(e => new { e.Status, e.CriadoEm })
            .HasDatabaseName("ix_robo_erro_status_criado");
    }
}
