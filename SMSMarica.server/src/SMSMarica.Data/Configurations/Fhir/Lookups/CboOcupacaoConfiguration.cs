using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Configurations.Fhir.Lookups;

internal sealed class CboOcupacaoConfiguration : IEntityTypeConfiguration<CboOcupacao>
{
    public void Configure(EntityTypeBuilder<CboOcupacao> b)
    {
        b.ToTable("cbo_ocupacao", schema: "fhir");
        b.HasKey(c => c.Codigo);
        b.Property(c => c.Codigo).HasColumnName("codigo").ValueGeneratedNever();
        b.Property(c => c.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        b.HasIndex(c => c.Titulo).HasDatabaseName("ix_cbo_ocupacao_titulo");
    }
}
