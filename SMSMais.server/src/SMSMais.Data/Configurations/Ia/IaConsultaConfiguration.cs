using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaConsultaConfiguration : IEntityTypeConfiguration<IaConsulta>
{
    public void Configure(EntityTypeBuilder<IaConsulta> builder)
    {
        builder.ToTable("ia_consulta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.Pergunta).HasColumnName("pergunta").IsRequired();
        builder.Property(x => x.SqlGerado).HasColumnName("sql_gerado");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.Visualizacao).HasColumnName("visualizacao").HasMaxLength(40);
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.Erro).HasColumnName("erro");
        builder.Property(x => x.TokensEntrada).HasColumnName("tokens_entrada");
        builder.Property(x => x.TokensSaida).HasColumnName("tokens_saida");
        builder.Property(x => x.DuracaoMs).HasColumnName("duracao_ms");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(x => x.Fonte).WithMany().HasForeignKey(x => x.FonteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CriadoEm);
    }
}
