using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaConsultaFeedbackConfiguration : IEntityTypeConfiguration<IaConsultaFeedback>
{
    public void Configure(EntityTypeBuilder<IaConsultaFeedback> builder)
    {
        builder.ToTable("ia_consulta_feedback");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.Familia).HasColumnName("familia").HasMaxLength(60);
        builder.Property(x => x.Pergunta).HasColumnName("pergunta").IsRequired();
        builder.Property(x => x.Resposta).HasColumnName("resposta");
        builder.Property(x => x.Util).HasColumnName("util").IsRequired();
        builder.Property(x => x.Comentario).HasColumnName("comentario");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.Resolucao).HasColumnName("resolucao");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.TratadoEm).HasColumnName("tratado_em");
        builder.Property(x => x.TratadoPor).HasColumnName("tratado_por");

        builder.HasOne(x => x.Fonte).WithMany().HasForeignKey(x => x.FonteId).OnDelete(DeleteBehavior.Cascade);
        // Fila de tratamento: pendentes mais recentes primeiro.
        builder.HasIndex(x => new { x.Status, x.CriadoEm });
        builder.HasIndex(x => x.Familia);
    }
}
