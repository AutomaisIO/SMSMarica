using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Data.Configurations;

internal sealed class MensagemWhatsAppConfiguration : IEntityTypeConfiguration<MensagemWhatsApp>
{
    public void Configure(EntityTypeBuilder<MensagemWhatsApp> builder)
    {
        builder.ToTable("tfd_mensagem_whatsapp");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.SessaoId).HasColumnName("sessao_id");
        builder.Property(m => m.PacienteId).HasColumnName("paciente_id");
        builder.Property(m => m.Telefone).HasColumnName("telefone").HasMaxLength(20).IsRequired();
        builder.Property(m => m.Template).HasColumnName("template").HasMaxLength(120);
        builder.Property(m => m.Direcao).HasColumnName("direcao").HasConversion<int>().IsRequired();
        builder.Property(m => m.Conteudo).HasColumnName("conteudo");
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(m => m.WaMessageId).HasColumnName("wa_message_id").HasMaxLength(120);
        builder.Property(m => m.ContextoWaMessageId).HasColumnName("contexto_wa_message_id").HasMaxLength(120);
        builder.Property(m => m.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasOne(m => m.Sessao).WithMany().HasForeignKey(m => m.SessaoId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.WaMessageId).IsUnique().HasFilter("wa_message_id IS NOT NULL");
        builder.HasIndex(m => new { m.PacienteId, m.OcorridoEm });
        builder.HasIndex(m => m.SessaoId);
    }
}
