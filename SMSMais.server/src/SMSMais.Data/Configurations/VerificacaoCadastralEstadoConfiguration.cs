using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class VerificacaoCadastralEstadoConfiguration : IEntityTypeConfiguration<VerificacaoCadastralEstado>
{
    public void Configure(EntityTypeBuilder<VerificacaoCadastralEstado> builder)
    {
        builder.ToTable("verificacao_cadastral_estado");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TelefoneCanonical).HasColumnName("telefone_canonical").HasMaxLength(20).IsRequired();
        builder.Property(e => e.ComunicacaoPacienteId).HasColumnName("comunicacao_paciente_id").IsRequired();
        builder.Property(e => e.PacienteId).HasColumnName("paciente_id");
        builder.Property(e => e.Etapa).HasColumnName("etapa").HasConversion<int>().IsRequired();
        builder.Property(e => e.CpfDigitosInformados).HasColumnName("cpf_digitos_informados").HasMaxLength(11);
        builder.Property(e => e.TentativasErradas).HasColumnName("tentativas_erradas").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.Reorientacoes).HasColumnName("reorientacoes").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(e => e.Comunicacao)
            .WithMany()
            .HasForeignKey(e => e.ComunicacaoPacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        // No máximo UM diálogo de verificação por telefone (molde agendamento_confirmacao_estado).
        builder.HasIndex(e => e.TelefoneCanonical)
            .HasDatabaseName("ux_verificacao_cadastral_telefone")
            .IsUnique();
    }
}
