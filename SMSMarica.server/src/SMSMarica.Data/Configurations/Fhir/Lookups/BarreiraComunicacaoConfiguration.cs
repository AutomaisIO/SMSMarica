using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Configurations.Fhir.Lookups;

internal sealed class BarreiraComunicacaoConfiguration : IEntityTypeConfiguration<BarreiraComunicacao>
{
    public void Configure(EntityTypeBuilder<BarreiraComunicacao> b)
    {
        b.ToTable("barreira_comunicacao", schema: "fhir");
        b.HasKey(x => x.Codigo);
        b.Property(x => x.Codigo).HasColumnName("codigo").ValueGeneratedNever();
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
    }
}
