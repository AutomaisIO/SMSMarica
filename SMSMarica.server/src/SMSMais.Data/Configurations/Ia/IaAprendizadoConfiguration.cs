using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Data.Configurations.Ia;

internal sealed class IaAprendizadoConfiguration : IEntityTypeConfiguration<IaAprendizado>
{
    public void Configure(EntityTypeBuilder<IaAprendizado> builder)
    {
        builder.ToTable("ia_aprendizado");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(x => x.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(x => x.Conteudo).HasColumnName("conteudo").IsRequired();
        builder.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("vector(1024)");
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(x => x.Fonte).WithMany().HasForeignKey(x => x.FonteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.FonteId, x.Ativo });
    }
}
