using Automais.Fhir.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Fhir.Data.Configurations;

public sealed class OrganizationRowConfiguration : IEntityTypeConfiguration<OrganizationRow>
{
    public void Configure(EntityTypeBuilder<OrganizationRow> builder)
    {
        builder.ToTable("organization");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");

        builder.Property(o => o.VersionId).HasColumnName("version_id").IsRequired();
        builder.Property(o => o.LastUpdated).HasColumnName("last_updated").IsRequired();
        builder.Property(o => o.MetaSource).HasColumnName("meta_source").IsRequired();
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Property(o => o.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();

        builder.Property(o => o.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(o => o.Cnes).HasColumnName("cnes").HasMaxLength(20);
        builder.Property(o => o.Ativa).HasColumnName("ativa").HasDefaultValue(true);
        builder.Property(o => o.PartOfId).HasColumnName("part_of_id");

        builder.Property(o => o.IdentifierSystem).HasColumnName("identifier_system").HasMaxLength(100);
        builder.Property(o => o.IdentifierValue).HasColumnName("identifier_value").HasMaxLength(200);

        // Busca natural por unidade. NÃO é único: o CNES pode mudar e, durante a transição,
        // conviver — o que é único é o par (system, value) do identifier.
        builder.HasIndex(o => o.Cnes).HasFilter("cnes IS NOT NULL");
        builder.HasIndex(o => o.PartOfId).HasFilter("part_of_id IS NOT NULL");
        builder.HasIndex(o => o.MetaSource);

        // Conditional update (ADR-0024/0039): no máximo UMA linha viva por identifier.
        builder.HasIndex(o => new { o.IdentifierSystem, o.IdentifierValue })
            .IsUnique()
            .HasFilter("identifier_system IS NOT NULL AND NOT is_deleted");
    }
}
