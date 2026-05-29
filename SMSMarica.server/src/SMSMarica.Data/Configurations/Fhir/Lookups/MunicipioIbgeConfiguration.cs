using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Configurations.Fhir.Lookups;

internal sealed class MunicipioIbgeConfiguration : IEntityTypeConfiguration<MunicipioIbge>
{
    public void Configure(EntityTypeBuilder<MunicipioIbge> b)
    {
        b.ToTable("municipio_ibge", schema: "fhir");
        b.HasKey(m => m.Codigo);
        b.Property(m => m.Codigo).HasColumnName("codigo").ValueGeneratedNever();
        b.Property(m => m.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.Property(m => m.Uf).HasColumnName("uf").HasMaxLength(2).IsRequired().IsFixedLength();
        b.HasIndex(m => m.Uf).HasDatabaseName("ix_municipio_ibge_uf");
        b.HasIndex(m => m.Nome).HasDatabaseName("ix_municipio_ibge_nome");
    }
}
