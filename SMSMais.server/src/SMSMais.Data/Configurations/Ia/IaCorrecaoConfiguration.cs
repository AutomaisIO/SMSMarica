using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaCorrecaoConfiguration : IEntityTypeConfiguration<IaCorrecao>
{
    public void Configure(EntityTypeBuilder<IaCorrecao> builder)
    {
        builder.ToTable("ia_correcao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ConsultaId).HasColumnName("consulta_id").IsRequired();
        builder.Property(x => x.AprendizadoId).HasColumnName("aprendizado_id");
        builder.Property(x => x.ErroOriginal).HasColumnName("erro_original").IsRequired();
        builder.Property(x => x.SqlAntes).HasColumnName("sql_antes");
        builder.Property(x => x.SqlDepois).HasColumnName("sql_depois");
        builder.Property(x => x.InstrucaoGerada).HasColumnName("instrucao_gerada");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.RevisadoEm).HasColumnName("revisado_em");
        builder.Property(x => x.RevisadoPor).HasColumnName("revisado_por");
        builder.Property(x => x.RemovidoEm).HasColumnName("removido_em");
        builder.Property(x => x.RemovidoPor).HasColumnName("removido_por");

        builder.HasOne(x => x.Consulta).WithMany().HasForeignKey(x => x.ConsultaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Aprendizado).WithMany().HasForeignKey(x => x.AprendizadoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ConsultaId);
    }
}
