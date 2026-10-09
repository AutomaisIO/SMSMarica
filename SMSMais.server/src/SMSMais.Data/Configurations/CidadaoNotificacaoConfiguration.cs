using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidadaoNotificacaoConfiguration : IEntityTypeConfiguration<CidadaoNotificacao>
{
    public void Configure(EntityTypeBuilder<CidadaoNotificacao> builder)
    {
        builder.ToTable("cidadao_notificacao");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.Property(n => n.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(n => n.Titulo).HasColumnName("titulo").HasMaxLength(65).IsRequired();
        builder.Property(n => n.Mensagem).HasColumnName("mensagem").HasMaxLength(240).IsRequired();
        builder.Property(n => n.Rota).HasColumnName("rota").HasMaxLength(40);
        builder.Property(n => n.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
        builder.Property(n => n.EnviadoPor).HasColumnName("enviado_por");
        builder.Property(n => n.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(n => n.Aparelhos).HasColumnName("aparelhos").IsRequired();
        builder.Property(n => n.Entregues).HasColumnName("entregues").IsRequired();
        builder.Property(n => n.Falha).HasColumnName("falha").HasMaxLength(500);

        builder.HasIndex(n => new { n.PacienteId, n.CriadoEm })
            .HasDatabaseName("ix_cidadao_notificacao_paciente");
    }
}
