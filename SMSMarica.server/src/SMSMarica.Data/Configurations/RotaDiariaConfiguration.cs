using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Configurations;

internal sealed class RotaDiariaConfiguration : IEntityTypeConfiguration<RotaDiaria>
{
    public void Configure(EntityTypeBuilder<RotaDiaria> builder)
    {
        builder.ToTable("translado_rota_diaria");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Data).HasColumnName("data").IsRequired();
        builder.Property(r => r.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(r => r.MotoristaId).HasColumnName("motorista_id").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(r => r.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(r => r.IniciadaEm).HasColumnName("iniciada_em");
        builder.Property(r => r.ConcluidaEm).HasColumnName("concluida_em");

        builder.Property(r => r.Origem).HasColumnName("origem").HasConversion<int>().HasDefaultValue(OrigemRota.Manual).IsRequired();
        builder.Property(r => r.GeradaEm).HasColumnName("gerada_em");
        builder.Property(r => r.DistanciaTotalMetros).HasColumnName("distancia_total_metros");
        builder.Property(r => r.DuracaoEstimadaSegundos).HasColumnName("duracao_estimada_segundos");
        builder.Property(r => r.PlanoRotaJson).HasColumnName("plano_rota_json").HasColumnType("jsonb");

        builder.HasOne(r => r.Veiculo).WithMany().HasForeignKey(r => r.VeiculoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Motorista).WithMany().HasForeignKey(r => r.MotoristaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Alocacoes)
            .WithOne(a => a.RotaDiaria)
            .HasForeignKey(a => a.RotaDiariaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.Data);
    }
}
