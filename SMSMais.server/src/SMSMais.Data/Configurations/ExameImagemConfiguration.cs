using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

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

        builder.Property(e => e.SiscanProtocolo).HasColumnName("siscan_protocolo").HasMaxLength(20);
        builder.Property(e => e.SiscanNumeroExame).HasColumnName("siscan_numero_exame").HasMaxLength(20);
        builder.Property(e => e.SiscanRequisicaoEm).HasColumnName("siscan_requisicao_em");
        builder.Property(e => e.SiscanRequisicaoPor).HasColumnName("siscan_requisicao_por");
        builder.Property(e => e.SiscanErro).HasColumnName("siscan_erro").HasMaxLength(1000);

        // Índice para a pergunta que a operação faz: "esse protocolo do SISCAN é de qual exame
        // nosso?". Filtrado porque a esmagadora maioria dos exames não tem requisição no SISCAN.
        builder.HasIndex(e => e.SiscanProtocolo)
            .HasDatabaseName("ix_exame_imagem_siscan_protocolo")
            .HasFilter("siscan_protocolo IS NOT NULL");

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

        // Um exame por código interno do pedido (ADR-0074). O 0..1 que antes valia para o pedido
        // vale agora para o PRINCIPAL; (pedido, ordem) não se repete. Sem filtro de excluido_em, como
        // era: um exame excluído continua ocupando a sua posição (e o seu accession).
        builder.Property(e => e.SolicitacaoPrincipalId).HasColumnName("solicitacao_principal_id");
        builder.Property(e => e.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.SolicitacaoProcedimentoId).HasColumnName("solicitacao_procedimento_id");
        builder.HasOne(e => e.SolicitacaoProcedimento)
            .WithMany().HasForeignKey(e => e.SolicitacaoProcedimentoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SolicitacaoId);
        builder.HasIndex(e => e.SolicitacaoPrincipalId).IsUnique();
        builder.HasIndex(e => new { e.SolicitacaoId, e.Ordem }).IsUnique();
        builder.HasIndex(e => e.SolicitacaoProcedimentoId).IsUnique()
            .HasFilter("solicitacao_procedimento_id IS NOT NULL");
        // O principal é sempre do próprio pedido e é o de ordem 0; extra nunca é principal.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_exame_imagem_principal",
            "(solicitacao_principal_id IS NULL AND ordem > 0) OR (solicitacao_principal_id = solicitacao_id AND ordem = 0)"));
        builder.HasIndex(e => e.AccessionNumber).IsUnique();
        builder.HasIndex(e => e.StudyInstanceUID).IsUnique();
        // Usado pelo worker de worklist (pega itens com tentativa vencida).
        builder.HasIndex(e => new { e.Status, e.ProximaTentativaEm });
    }
}
