using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class PosicaoVeiculoConfiguration : IEntityTypeConfiguration<PosicaoVeiculo>
{
    public void Configure(EntityTypeBuilder<PosicaoVeiculo> builder)
    {
        builder.ToTable("rastreamento_posicao_veiculo");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(p => p.DispositivoId).HasColumnName("dispositivo_id").IsRequired();
        builder.Property(p => p.VelocidadeKmh).HasColumnName("velocidade_kmh");
        builder.Property(p => p.Rumo).HasColumnName("rumo");
        builder.Property(p => p.PrecisaoM).HasColumnName("precisao_m");
        builder.Property(p => p.CapturadoEm).HasColumnName("capturado_em").IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.OwnsOne(p => p.Coordenada, gps =>
        {
            gps.Property(g => g.Latitude).HasColumnName("latitude").IsRequired();
            gps.Property(g => g.Longitude).HasColumnName("longitude").IsRequired();
        });

        // Sem FK para o dispositivo: o histórico do carro fica mesmo se o tablet for trocado.
        builder.HasOne<Veiculo>().WithMany().HasForeignKey(p => p.VeiculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.VeiculoId, p.CapturadoEm });
    }
}
