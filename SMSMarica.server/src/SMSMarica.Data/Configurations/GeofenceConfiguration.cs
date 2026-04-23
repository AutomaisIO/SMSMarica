using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class GeofenceConfiguration : IEntityTypeConfiguration<Geofence>
{
    public void Configure(EntityTypeBuilder<Geofence> builder)
    {
        builder.ToTable("rastreamento_geofence");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(g => g.ReferenciaId).HasColumnName("referencia_id").IsRequired();
        builder.Property(g => g.RaioMetros).HasColumnName("raio_metros").IsRequired();
        builder.Property(g => g.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.OwnsOne(g => g.Centro, gps =>
        {
            gps.Property(c => c.Latitude).HasColumnName("centro_latitude").IsRequired();
            gps.Property(c => c.Longitude).HasColumnName("centro_longitude").IsRequired();
        });

        builder.HasIndex(g => new { g.Tipo, g.ReferenciaId });
    }
}
