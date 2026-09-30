using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class AcompanhanteConfiguration : IEntityTypeConfiguration<Acompanhante>
{
    public void Configure(EntityTypeBuilder<Acompanhante> builder)
    {
        builder.ToTable("acompanhante");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(a => a.Cpf).HasColumnName("cpf").HasMaxLength(11).IsFixedLength().IsRequired();
        builder.Property(a => a.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(a => a.DataNascimento).HasColumnName("data_nascimento").IsRequired();
        builder.Property(a => a.Parentesco).HasColumnName("parentesco").HasConversion<int>();
        builder.Property(a => a.Telefone).HasColumnName("telefone").HasMaxLength(20);
        builder.Property(a => a.PacienteVinculadoId).HasColumnName("paciente_vinculado_id");
        builder.Property(a => a.FonteNome).HasColumnName("fonte_nome").HasConversion<int>().IsRequired();
        builder.Property(a => a.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(a => a.ExcluidoPor).HasColumnName("excluido_por");

        // PacienteId e PacienteVinculadoId referenciam fhir.patient (hub FHIR) — sem FK local.
        // Uma pessoa entra uma vez só na lista de cada paciente (entre as não excluídas).
        builder.HasIndex(a => new { a.PacienteId, a.Cpf })
            .IsUnique()
            .HasFilter("excluido_em IS NULL");
    }
}
