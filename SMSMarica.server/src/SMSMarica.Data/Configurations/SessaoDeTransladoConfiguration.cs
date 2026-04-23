using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class SessaoDeTransladoConfiguration : IEntityTypeConfiguration<SessaoDeTranslado>
{
    public void Configure(EntityTypeBuilder<SessaoDeTranslado> builder)
    {
        builder.ToTable("translado_sessao");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TratamentoId).HasColumnName("tratamento_id").IsRequired();
        builder.Property(s => s.DataPrevista).HasColumnName("data_prevista").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(s => s.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(s => new { s.TratamentoId, s.DataPrevista });
        builder.HasIndex(s => s.DataPrevista);
    }
}
