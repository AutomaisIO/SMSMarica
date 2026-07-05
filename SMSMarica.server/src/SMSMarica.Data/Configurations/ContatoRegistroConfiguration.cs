using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class ContatoRegistroConfiguration : IEntityTypeConfiguration<ContatoRegistro>
{
    public void Configure(EntityTypeBuilder<ContatoRegistro> builder)
    {
        builder.ToTable("contato_registro");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SolicitacaoExameId).HasColumnName("solicitacao_exame_id").IsRequired();
        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.Property(c => c.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(c => c.Meio).HasColumnName("meio").HasConversion<int>().IsRequired();
        builder.Property(c => c.Resultado).HasColumnName("resultado").HasConversion<int>().IsRequired();
        builder.Property(c => c.Observacao).HasColumnName("observacao").HasMaxLength(500);
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(c => c.SolicitacaoExame)
            .WithMany()
            .HasForeignKey(c => c.SolicitacaoExameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.SolicitacaoExameId, c.CriadoEm });
    }
}
