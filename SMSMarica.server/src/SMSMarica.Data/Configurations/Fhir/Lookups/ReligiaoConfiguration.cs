using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Configurations.Fhir.Lookups;

internal sealed class ReligiaoConfiguration : IEntityTypeConfiguration<Religiao>
{
    public void Configure(EntityTypeBuilder<Religiao> b)
    {
        b.ToTable("religiao", schema: "fhir");
        b.HasKey(r => r.Codigo);
        b.Property(r => r.Codigo).HasColumnName("codigo").ValueGeneratedNever();
        b.Property(r => r.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
    }
}
