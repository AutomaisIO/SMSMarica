using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Seeds;

namespace SMSMarica.Data.Configurations;

internal sealed class ProcedimentoSigtapConfiguration : IEntityTypeConfiguration<ProcedimentoSigtap>
{
    public void Configure(EntityTypeBuilder<ProcedimentoSigtap> builder)
    {
        builder.ToTable("procedimento_sigtap");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Codigo).HasColumnName("codigo").HasMaxLength(15).IsRequired();
        builder.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(250).IsRequired();
        builder.Property(p => p.Grupo).HasColumnName("grupo").HasMaxLength(60).IsRequired();
        builder.Property(p => p.Subgrupo).HasColumnName("subgrupo").HasMaxLength(60).IsRequired();
        builder.Property(p => p.Forma).HasColumnName("forma").HasMaxLength(60).IsRequired();
        builder.Property(p => p.Descricao).HasColumnName("descricao").HasColumnType("text").IsRequired();
        builder.Property(p => p.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(p => p.CompetenciaInicio).HasColumnName("competencia_inicio").IsRequired();
        builder.Property(p => p.CompetenciaFim).HasColumnName("competencia_fim");

        builder.HasIndex(p => p.Codigo).IsUnique();
        builder.HasIndex(p => new { p.Grupo, p.Subgrupo });
        builder.HasIndex(p => p.Ativo);

        builder.HasData(ProcedimentosSigtapSeed.Itens);
    }
}
