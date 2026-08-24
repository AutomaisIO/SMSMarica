using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class MensagemWhatsAppConfiguration : IEntityTypeConfiguration<MensagemWhatsApp>
{
    public void Configure(EntityTypeBuilder<MensagemWhatsApp> builder)
    {
        builder.ToTable("whatsapp_mensagem");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.PacienteId).HasColumnName("paciente_id");
        builder.Property(m => m.Telefone).HasColumnName("telefone").HasMaxLength(20).IsRequired();
        builder.Property(m => m.Template).HasColumnName("template").HasMaxLength(120);
        builder.Property(m => m.Direcao).HasColumnName("direcao").HasConversion<int>().IsRequired();
        builder.Property(m => m.Conteudo).HasColumnName("conteudo");
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(m => m.WaMessageId).HasColumnName("wa_message_id").HasMaxLength(120);
        builder.Property(m => m.ContextoWaMessageId).HasColumnName("contexto_wa_message_id").HasMaxLength(120);
        builder.Property(m => m.ErroMeta).HasColumnName("erro_meta").HasMaxLength(500);
        builder.Property(m => m.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Módulo Conversas (aditivo, nullable) — ver EstenderMensagemWhatsAppParaConversa.
        builder.Property(m => m.ConversaId).HasColumnName("conversa_id");
        builder.Property(m => m.AutorUsuarioId).HasColumnName("autor_usuario_id");
        builder.Property(m => m.AutorNomeExibicao).HasColumnName("autor_nome_exibicao").HasMaxLength(200);
        builder.Property(m => m.TipoMensagem).HasColumnName("tipo_mensagem").HasConversion<int>();

        builder.HasOne(m => m.Conversa).WithMany(c => c.Mensagens).HasForeignKey(m => m.ConversaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.WaMessageId).IsUnique().HasFilter("wa_message_id IS NOT NULL");
        builder.HasIndex(m => new { m.PacienteId, m.OcorridoEm });
        builder.HasIndex(m => new { m.ConversaId, m.OcorridoEm });
    }
}
