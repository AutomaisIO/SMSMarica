using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Data.Configurations.Sernit;

internal sealed class SernitVarreduraFalhaConfiguration : IEntityTypeConfiguration<SernitVarreduraFalha>
{
    public void Configure(EntityTypeBuilder<SernitVarreduraFalha> builder)
    {
        builder.ToTable("sernit_varredura_falha");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.Situacao).HasColumnName("situacao");
        builder.Property(x => x.FatiaInicio).HasColumnName("fatia_inicio");
        builder.Property(x => x.FatiaFim).HasColumnName("fatia_fim");
        builder.Property(x => x.TipoRecurso).HasColumnName("tipo_recurso");
        builder.Property(x => x.IdSernit).HasColumnName("id_sernit").HasMaxLength(20);
        builder.Property(x => x.Mensagem).HasColumnName("mensagem").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Detalhe).HasColumnName("detalhe");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(x => new { x.ExecucaoId, x.Tipo })
            .HasDatabaseName("ix_sernit_varredura_falha_execucao_tipo");

        builder.HasOne(x => x.Execucao)
            .WithMany()
            .HasForeignKey(x => x.ExecucaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
