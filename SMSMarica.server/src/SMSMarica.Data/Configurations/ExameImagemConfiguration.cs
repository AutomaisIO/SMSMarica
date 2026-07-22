using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

/// <summary>
/// Satélite de execução de imagem (ADR-0021). Fica com as colunas DICOM/PACS/worklist que hoje
/// moram em <c>solicitacao_exame</c>; a regulação vai para a espinha <c>solicitacao</c>.
/// </summary>
internal sealed class ExameImagemConfiguration : IEntityTypeConfiguration<ExameImagem>
{
    public void Configure(EntityTypeBuilder<ExameImagem> builder)
    {
        builder.ToTable("exame_imagem");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(e => e.TipoExameId).HasColumnName("tipo_exame_id"); // NULLABLE = mapeamento pendente

        builder.Property(e => e.AccessionNumber).HasColumnName("accession_number").HasMaxLength(16).IsRequired();
        builder.Property(e => e.StudyInstanceUID).HasColumnName("study_instance_uid").HasMaxLength(128).IsRequired();
        builder.Property(e => e.WorklistItemUid).HasColumnName("worklist_item_uid").HasMaxLength(128);

        // Estação escolhida (quando a unidade tem mais de um equipamento na modalidade).
        // Restrict: equipamento com exame apontando para ele não some do histórico.
        builder.Property(e => e.EquipamentoId).HasColumnName("equipamento_id");
        builder.HasOne(e => e.Equipamento)
            .WithMany()
            .HasForeignKey(e => e.EquipamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(e => e.IniciadoEm).HasColumnName("iniciado_em");
        builder.Property(e => e.RealizadoEm).HasColumnName("realizado_em");
        // DataEstudo = wall-clock do DICOM (Kind=Unspecified) → timestamp without time zone.
        builder.Property(e => e.DataEstudo).HasColumnName("data_estudo").HasColumnType("timestamp without time zone");

        builder.Property(e => e.ErroIntegracaoPacs).HasColumnName("erro_integracao_pacs").HasMaxLength(1000);
        builder.Property(e => e.TentativasEnvio).HasColumnName("tentativas_envio").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.UltimaTentativaEm).HasColumnName("ultima_tentativa_em");
        builder.Property(e => e.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");

        builder.Property(e => e.ImagensPreparadasEm).HasColumnName("imagens_preparadas_em");
        builder.Property(e => e.ImagensPreparacaoTentativas).HasColumnName("imagens_preparacao_tentativas").HasDefaultValue(0).IsRequired();

        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por");
        builder.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(e => e.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(e => e.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(e => e.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(e => e.RowVersion)
            .HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasOne(e => e.TipoExame)
            .WithMany().HasForeignKey(e => e.TipoExameId).OnDelete(DeleteBehavior.Restrict);

        // 1 solicitação de imagem : 1 execução (índice único garante o 0..1).
        builder.HasIndex(e => e.SolicitacaoId).IsUnique();
        builder.HasIndex(e => e.AccessionNumber).IsUnique();
        builder.HasIndex(e => e.StudyInstanceUID).IsUnique();
        // Usado pelo worker de worklist (pega itens com tentativa vencida).
        builder.HasIndex(e => new { e.Status, e.ProximaTentativaEm });
    }
}
