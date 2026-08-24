using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class PontoGpsConfiguration : IEntityTypeConfiguration<PontoGps>
{
    public void Configure(EntityTypeBuilder<PontoGps> builder)
    {
        builder.ToTable("rastreamento_ponto_gps");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.MotoristaId).HasColumnName("motorista_id").IsRequired();
        builder.Property(p => p.CapturadoEm).HasColumnName("capturado_em").IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.OwnsOne(p => p.Coordenada, gps =>
        {
            gps.Property(g => g.Latitude).HasColumnName("latitude").IsRequired();
            gps.Property(g => g.Longitude).HasColumnName("longitude").IsRequired();
        });

        builder.HasOne(p => p.Motorista).WithMany().HasForeignKey(p => p.MotoristaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.MotoristaId, p.CapturadoEm });
    }
}
