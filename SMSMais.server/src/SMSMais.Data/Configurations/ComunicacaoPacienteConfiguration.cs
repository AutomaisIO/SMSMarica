using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class ComunicacaoPacienteConfiguration : IEntityTypeConfiguration<ComunicacaoPaciente>
{
    public void Configure(EntityTypeBuilder<ComunicacaoPaciente> builder)
    {
        builder.ToTable("comunicacao_paciente");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        // Default 1 (ConfirmacaoAgendamento) backfilla as linhas anteriores ao rename.
        builder.Property(n => n.Finalidade).HasColumnName("finalidade").HasConversion<int>()
            .HasDefaultValue(Entities.Enums.FinalidadeComunicacao.ConfirmacaoAgendamento).IsRequired();
        builder.Property(n => n.SolicitacaoId).HasColumnName("solicitacao_id");
        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.Property(n => n.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(n => n.Telefone).HasColumnName("telefone").HasMaxLength(20);
        builder.Property(n => n.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(n => n.MotivoFalha).HasColumnName("motivo_falha").HasMaxLength(1000);
        builder.Property(n => n.Origem).HasColumnName("origem").HasConversion<int>()
            .HasDefaultValue(Entities.Enums.OrigemComunicacao.Automatico).IsRequired();
        builder.Property(n => n.EnviadoPor).HasColumnName("enviado_por");
        builder.Property(n => n.IgnorarVerificacaoTelefone)
            .HasColumnName("ignorar_verificacao_telefone").HasDefaultValue(false).IsRequired();
        builder.Property(n => n.IgnorarJanelaHorario)
            .HasColumnName("ignorar_janela_horario").HasDefaultValue(false).IsRequired();
        builder.Property(n => n.LoginLinkId).HasColumnName("login_link_id");
        builder.Property(n => n.MensagemWhatsAppId).HasColumnName("mensagem_whatsapp_id");
        builder.Property(n => n.Tentativas).HasColumnName("tentativas").HasDefaultValue(0).IsRequired();
        builder.Property(n => n.UltimaTentativaEm).HasColumnName("ultima_tentativa_em");
        builder.Property(n => n.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");
        builder.Property(n => n.EnviadoEm).HasColumnName("enviado_em");
        builder.Property(n => n.EntregueEm).HasColumnName("entregue_em");
        builder.Property(n => n.LidoEm).HasColumnName("lido_em");
        builder.Property(n => n.VisualizadoEm).HasColumnName("visualizado_em");
        builder.Property(n => n.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(n => n.AtualizadoEm).HasColumnName("atualizado_em");

        builder.Property(n => n.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(n => n.Solicitacao)
            .WithMany()
            .HasForeignKey(n => n.SolicitacaoId)
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
        // Uma comunicação por solicitação × finalidade.
        builder.HasIndex(n => new { n.SolicitacaoId, n.Finalidade })
            .IsUnique()
            .HasFilter("solicitacao_id IS NOT NULL");
        builder.HasIndex(n => n.MensagemWhatsAppId);
        builder.HasIndex(n => n.PacienteId);
    }
}
