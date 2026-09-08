using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class TipoExameUnidadeConfiguration : IEntityTypeConfiguration<TipoExameUnidade>
{
    public void Configure(EntityTypeBuilder<TipoExameUnidade> builder)
    {
        builder.ToTable("tipo_exame_unidade");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TipoExameId).HasColumnName("tipo_exame_id").IsRequired();
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.EnviarParaWorklist).HasColumnName("enviar_para_worklist").IsRequired();
        builder.Property(x => x.EquipamentoId).HasColumnName("equipamento_id");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(x => x.TipoExame)
            .WithMany()
            .HasForeignKey(x => x.TipoExameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Unidade)
            .WithMany()
            .HasForeignKey(x => x.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, não SetNull: desativar um aparelho não pode apagar em silêncio a configuração
        // de destino. O service impede excluir equipamento em uso e manda desativar.
        builder.HasOne(x => x.Equipamento)
            .WithMany()
            .HasForeignKey(x => x.EquipamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Um par por unidade. Filtrado pelo soft-delete para que reincluir um exame que saiu do
        // escopo não esbarre na linha excluída.
        builder.HasIndex(x => new { x.TipoExameId, x.UnidadeId })
            .HasDatabaseName("ux_tipo_exame_unidade")
            .IsUnique()
            .HasFilter("excluido_em IS NULL");

        // A pergunta quente do worker e da tela: "o que esta unidade manda para a worklist?".
        builder.HasIndex(x => new { x.UnidadeId, x.EnviarParaWorklist })
            .HasDatabaseName("ix_tipo_exame_unidade_unidade_envio")
            .HasFilter("excluido_em IS NULL");
    }
}
