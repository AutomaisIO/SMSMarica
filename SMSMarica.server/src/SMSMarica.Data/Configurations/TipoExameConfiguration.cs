using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class TipoExameConfiguration : IEntityTypeConfiguration<TipoExame>
{
    public void Configure(EntityTypeBuilder<TipoExame> builder)
    {
        builder.ToTable("tipo_exame");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(t => t.CodigoSisreg).HasColumnName("codigo_sisreg").HasMaxLength(10);
        builder.Property(t => t.AutoCriado).HasColumnName("auto_criado").HasDefaultValue(false).IsRequired();
        // Correlação de faturamento, não identidade — por isso nullable desde 10/08/2026.
        builder.Property(t => t.ProcedimentoSigtapId).HasColumnName("procedimento_sigtap_id");
        builder.Property(t => t.ModalidadeDicom).HasColumnName("modalidade_dicom").HasConversion<int>().IsRequired();
        builder.Property(t => t.RequestedProcedureDescription).HasColumnName("requested_procedure_description").HasMaxLength(200).IsRequired();
        builder.Property(t => t.ScheduledProcedureStepDescription).HasColumnName("scheduled_procedure_step_description").HasMaxLength(200).IsRequired();
        builder.Property(t => t.CodigosProtocolo).HasColumnName("codigos_protocolo").HasColumnType("text[]").IsRequired();
        builder.Property(t => t.TempoEstimadoMinutos).HasColumnName("tempo_estimado_minutos");
        builder.Property(t => t.UnidadePadraoId).HasColumnName("unidade_padrao_id");
        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.EnviarParaWorklist).HasColumnName("enviar_para_worklist").HasDefaultValue(true).IsRequired();

        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.CriadoPor).HasColumnName("criado_por");
        builder.Property(t => t.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(t => t.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(t => t.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(t => t.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(t => t.ProcedimentoSigtap)
            .WithMany()
            .HasForeignKey(t => t.ProcedimentoSigtapId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.UnidadePadrao)
            .WithMany()
            .HasForeignKey(t => t.UnidadePadraoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nome único entre não-excluídos — e o nome É a chave do procedimento do SISREG, então este
        // índice é o que garante que a criação automática não duplique tipo a cada importação.
        builder.HasIndex(t => t.Nome).IsUnique().HasFilter("excluido_em IS NULL");
        // NÃO é único: o SISREG manda o `pa` vazio em ~1/3 das linhas, e o mesmo procedimento
        // aparece ora com código, ora sem. Índice só para busca.
        builder.HasIndex(t => t.CodigoSisreg).HasFilter("codigo_sisreg IS NOT NULL");
        builder.HasIndex(t => t.ProcedimentoSigtapId);
        builder.HasIndex(t => t.ModalidadeDicom);
        builder.HasIndex(t => t.Ativo);

        // Seed inicial de TipoExame é aplicado via SQL puro em
        // Migrations/20260525011830_SeedTiposExameIniciais.cs — HasData não foi
        // usado porque a List<string> de codigos_protocolo dispara
        // PendingModelChangesWarning a cada build.
    }
}
