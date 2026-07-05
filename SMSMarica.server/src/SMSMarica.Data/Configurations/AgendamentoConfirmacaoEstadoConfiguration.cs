using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class AgendamentoConfirmacaoEstadoConfiguration : IEntityTypeConfiguration<AgendamentoConfirmacaoEstado>
{
    public void Configure(EntityTypeBuilder<AgendamentoConfirmacaoEstado> builder)
    {
        builder.ToTable("agendamento_confirmacao_estado");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TelefoneCanonical).HasColumnName("telefone_canonical").HasMaxLength(20).IsRequired();
        builder.Property(e => e.ComunicacaoPacienteId).HasColumnName("comunicacao_paciente_id").IsRequired();
        builder.Property(e => e.Etapa).HasColumnName("etapa").HasConversion<int>().IsRequired();
        builder.Property(e => e.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(e => e.ComunicacaoPaciente)
            .WithMany()
            .HasForeignKey(e => e.ComunicacaoPacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um estado ativo por telefone (o handler substitui/remove ao concluir).
        builder.HasIndex(e => e.TelefoneCanonical).IsUnique();
    }
}
