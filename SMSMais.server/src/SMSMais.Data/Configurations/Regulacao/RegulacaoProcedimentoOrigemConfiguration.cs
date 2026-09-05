using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoProcedimentoOrigemConfiguration
    : IEntityTypeConfiguration<RegulacaoProcedimentoOrigem>
{
    public void Configure(EntityTypeBuilder<RegulacaoProcedimentoOrigem> builder)
    {
        builder.ToTable("regulacao_procedimento_origem");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProcedimentoId).HasColumnName("procedimento_id").IsRequired();
        builder.Property(x => x.Sistema).HasColumnName("sistema").IsRequired();
        builder.Property(x => x.ChaveExterna).HasColumnName("chave_externa").HasMaxLength(120).IsRequired();
        builder.Property(x => x.RotuloExterno).HasColumnName("rotulo_externo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Ramo).HasColumnName("ramo").HasMaxLength(20);

        builder.Property(x => x.SisregProcedimentoSigtapId).HasColumnName("sisreg_procedimento_sigtap_id");
        builder.Property(x => x.SerCatalogoRecursoId).HasColumnName("ser_catalogo_recurso_id");
        builder.Property(x => x.SernitCatalogoRecursoId).HasColumnName("sernit_catalogo_recurso_id");

        builder.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("vector(1024)");
        builder.Property(x => x.EmbeddingHash).HasColumnName("embedding_hash").HasMaxLength(64);
        builder.Property(x => x.EmbeddingEm).HasColumnName("embedding_em");

        builder.Property(x => x.Vinculo).HasColumnName("vinculo").IsRequired();
        builder.Property(x => x.SugeridoProcedimentoId).HasColumnName("sugerido_procedimento_id");
        builder.Property(x => x.SugeridoScore).HasColumnName("sugerido_score");
        builder.Property(x => x.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(x => x.ConfirmadoPor).HasColumnName("confirmado_por");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(x => x.Procedimento)
            .WithMany(p => p.Origens)
            .HasForeignKey(x => x.ProcedimentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // O catálogo de sistema é espelho e pode ser recarregado; a origem sobrevive a isso e
        // guarda o rótulo que a solicitação viu. Por isso `SetNull` e não `Cascade`.
        builder.HasOne<SisregProcedimentoSigtap>()
            .WithMany()
            .HasForeignKey(x => x.SisregProcedimentoSigtapId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SerCatalogoRecurso>()
            .WithMany()
            .HasForeignKey(x => x.SerCatalogoRecursoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SernitCatalogoRecurso>()
            .WithMany()
            .HasForeignKey(x => x.SernitCatalogoRecursoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ProcedimentoId)
            .HasDatabaseName("ix_regulacao_proc_origem_procedimento");

        builder.HasIndex(x => new { x.Sistema, x.Ativo })
            .HasDatabaseName("ix_regulacao_proc_origem_sistema_ativo");

        // A chave externa É a identidade da origem no seu sistema, e é por ela que o upsert do
        // sync decide entre criar e atualizar. Sem o unique, uma segunda passada do sync
        // duplicaria o catálogo inteiro em silêncio.
        builder.HasIndex(x => new { x.Sistema, x.ChaveExterna })
            .IsUnique()
            .HasDatabaseName("ux_regulacao_proc_origem_sistema_chave");
    }
}
