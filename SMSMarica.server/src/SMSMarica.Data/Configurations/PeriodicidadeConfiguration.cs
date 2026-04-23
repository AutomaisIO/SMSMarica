using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class PeriodicidadeConfiguration : IEntityTypeConfiguration<Periodicidade>
{
    public void Configure(EntityTypeBuilder<Periodicidade> builder)
    {
        builder.ToTable("tratamento_periodicidade");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TratamentoId).HasColumnName("tratamento_id").IsRequired();
        builder.Property(p => p.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(p => p.IntervaloDias).HasColumnName("intervalo_dias");
        builder.Property(p => p.DiasSemanaMascara).HasColumnName("dias_semana_mascara");
        builder.Property(p => p.DataInicio).HasColumnName("data_inicio").IsRequired();
        builder.Property(p => p.QuantidadeSessoes).HasColumnName("quantidade_sessoes").IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(p => p.TratamentoId).IsUnique();
    }
}
