using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class WabaConfiguration : IEntityTypeConfiguration<Waba>
{
    public void Configure(EntityTypeBuilder<Waba> b)
    {
        b.ToTable("waba");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.WabaId).HasColumnName("waba_id").HasMaxLength(60).IsRequired();
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200);
        b.Property(x => x.DestinoId).HasColumnName("destino_id");
        b.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(500);
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em");

        b.HasIndex(x => x.WabaId).IsUnique().HasDatabaseName("ux_waba_waba_id");

        b.HasOne(x => x.Destino)
            .WithMany()
            .HasForeignKey(x => x.DestinoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
