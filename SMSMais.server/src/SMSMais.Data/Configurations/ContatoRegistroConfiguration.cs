using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class ContatoRegistroConfiguration : IEntityTypeConfiguration<ContatoRegistro>
{
    public void Configure(EntityTypeBuilder<ContatoRegistro> builder)
    {
        builder.ToTable("contato_registro");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.Property(c => c.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(c => c.Meio).HasColumnName("meio").HasConversion<int>().IsRequired();
        builder.Property(c => c.Resultado).HasColumnName("resultado").HasConversion<int>().IsRequired();
        builder.Property(c => c.Observacao).HasColumnName("observacao").HasMaxLength(500);
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(c => c.Solicitacao)
            .WithMany()
            .HasForeignKey(c => c.SolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.SolicitacaoId, c.CriadoEm });
    }
}
