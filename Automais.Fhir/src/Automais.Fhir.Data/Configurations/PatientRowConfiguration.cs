using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class PatientRowConfiguration : IEntityTypeConfiguration<PatientRow>
{
    public void Configure(EntityTypeBuilder<PatientRow> builder)
    {
        builder.ToTable("patient");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(p => p.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(p => p.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // O recurso FHIR completo (document-store).
        builder.Property(p => p.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        // Search params extraídos do documento.
        builder.Property(p => p.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(p => p.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(300);
        builder.Property(p => p.Telefone).HasColumnName("telefone").HasMaxLength(400);
        builder.Property(p => p.Nascimento).HasColumnName("nascimento");

        builder.HasIndex(p => p.Cpf).HasFilter("cpf IS NOT NULL");
        builder.HasIndex(p => p.Cns).HasFilter("cns IS NOT NULL");
        builder.HasIndex(p => p.Nome);
        builder.HasIndex(p => p.Telefone);
        builder.HasIndex(p => p.MetaSource);
    }
}
