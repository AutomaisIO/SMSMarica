using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Configurations.Fhir.Lookups;

internal sealed class PaisIsoConfiguration : IEntityTypeConfiguration<PaisIso>
{
    public void Configure(EntityTypeBuilder<PaisIso> b)
    {
        b.ToTable("pais_iso", schema: "fhir");
        b.HasKey(p => p.CodigoAlfa3);
        b.Property(p => p.CodigoAlfa3).HasColumnName("codigo_alfa3").HasMaxLength(3).IsFixedLength();
        b.Property(p => p.CodigoAlfa2).HasColumnName("codigo_alfa2").HasMaxLength(2).IsFixedLength().IsRequired();
        b.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.HasIndex(p => p.CodigoAlfa2).IsUnique();
    }
}
