using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Automais.Fhir.Data.Entities;

namespace Automais.Fhir.Data.Configurations;

public sealed class PractitionerRowConfiguration : IEntityTypeConfiguration<PractitionerRow>
{
    public void Configure(EntityTypeBuilder<PractitionerRow> builder)
    {
        builder.ToTable("practitioner");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(p => p.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(p => p.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(p => p.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(p => p.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(p => p.Conselho).HasColumnName("conselho").HasMaxLength(20);
        builder.Property(p => p.Registro).HasColumnName("registro").HasMaxLength(20);
        builder.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(300);

        builder.HasIndex(p => p.Cpf).HasFilter("cpf IS NOT NULL");
        builder.HasIndex(p => p.Conselho).HasFilter("conselho IS NOT NULL");
        builder.HasIndex(p => p.Registro).HasFilter("registro IS NOT NULL");
        builder.HasIndex(p => p.Nome);
        builder.HasIndex(p => p.MetaSource);
    }
}
