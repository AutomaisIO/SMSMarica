using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.AgenteIa;

namespace SMSMais.Data.Configurations;

internal sealed class AgenteWhatsAppPedidoConfiguration : IEntityTypeConfiguration<AgenteWhatsAppPedido>
{
    public void Configure(EntityTypeBuilder<AgenteWhatsAppPedido> builder)
    {
        builder.ToTable("agente_whatsapp_pedido");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Telefone).HasColumnName("telefone").HasMaxLength(20).IsRequired();
        builder.Property(e => e.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(e => e.MensagemId).HasColumnName("mensagem_id").IsRequired();
        builder.Property(e => e.Texto).HasColumnName("texto").HasColumnType("text").IsRequired();
        builder.Property(e => e.Citado).HasColumnName("citado").HasColumnType("text");
        builder.Property(e => e.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(e => e.SessaoId).HasColumnName("sessao_id").HasMaxLength(64);
        builder.Property(e => e.TurnoId).HasColumnName("turno_id").HasMaxLength(64);
        builder.Property(e => e.Cursor).HasColumnName("cursor").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.AndamentoPendente).HasColumnName("andamento_pendente").HasColumnType("text");
        builder.Property(e => e.UltimoAndamentoEm).HasColumnName("ultimo_andamento_em");
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.IniciadoEm).HasColumnName("iniciado_em");
        builder.Property(e => e.ConcluidoEm).HasColumnName("concluido_em");
        builder.Property(e => e.Erro).HasColumnName("erro").HasColumnType("text");

        builder.HasIndex(e => e.MensagemId).IsUnique().HasDatabaseName("ux_agente_whatsapp_pedido_mensagem");
        builder.HasIndex(e => new { e.Situacao, e.CriadoEm }).HasDatabaseName("ix_agente_whatsapp_pedido_situacao");
    }
}
