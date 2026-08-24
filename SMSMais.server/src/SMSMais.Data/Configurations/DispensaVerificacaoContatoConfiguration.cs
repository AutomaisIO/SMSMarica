using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class DispensaVerificacaoContatoConfiguration
    : IEntityTypeConfiguration<DispensaVerificacaoContato>
{
    public void Configure(EntityTypeBuilder<DispensaVerificacaoContato> builder)
    {
        builder.ToTable("dispensa_verificacao_contato");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.Property(d => d.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(d => d.Motivo).HasColumnName("motivo").HasConversion<int>().IsRequired();
        builder.Property(d => d.MotivoDescricao).HasColumnName("motivo_descricao").HasMaxLength(1000);
        builder.Property(d => d.PacienteCiente).HasColumnName("paciente_ciente").IsRequired();
        builder.Property(d => d.TelefoneNaEpoca).HasColumnName("telefone_na_epoca").HasMaxLength(20);
        builder.Property(d => d.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(d => d.CriadoPor).HasColumnName("criado_por");
        builder.Property(d => d.RevogadoEm).HasColumnName("revogado_em");
        builder.Property(d => d.RevogadoPor).HasColumnName("revogado_por");
        builder.Property(d => d.RevogadoMotivo).HasColumnName("revogado_motivo").HasMaxLength(500);

        // No máximo UMA dispensa ativa por paciente. As revogadas ficam para a trilha.
        builder.HasIndex(d => d.PacienteId)
            .IsUnique()
            .HasFilter("revogado_em IS NULL")
            .HasDatabaseName("ux_dispensa_verificacao_contato_paciente_ativa");

        // Histórico do paciente em ordem (tela de auditoria do contato).
        builder.HasIndex(d => new { d.PacienteId, d.CriadoEm });
    }
}
