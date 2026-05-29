using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Configurations.Fhir.Lookups;

internal sealed class EtniaIndigenaConfiguration : IEntityTypeConfiguration<EtniaIndigena>
{
    public void Configure(EntityTypeBuilder<EtniaIndigena> b)
    {
        b.ToTable("etnia_indigena", schema: "fhir");
        b.HasKey(e => e.Codigo);
        b.Property(e => e.Codigo).HasColumnName("codigo").ValueGeneratedNever();
        b.Property(e => e.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
    }
}
