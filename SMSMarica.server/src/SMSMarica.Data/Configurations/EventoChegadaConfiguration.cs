using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class EventoChegadaConfiguration : IEntityTypeConfiguration<EventoChegada>
{
    public void Configure(EntityTypeBuilder<EventoChegada> builder)
    {
        builder.ToTable("rastreamento_evento_chegada");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RotaDiariaId).HasColumnName("rota_diaria_id").IsRequired();
        builder.Property(e => e.GeofenceId).HasColumnName("geofence_id").IsRequired();
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasOne(e => e.RotaDiaria).WithMany().HasForeignKey(e => e.RotaDiariaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Geofence).WithMany().HasForeignKey(e => e.GeofenceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RotaDiariaId, e.OcorridoEm });
    }
}
