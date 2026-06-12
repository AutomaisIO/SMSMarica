using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class AnamneseConfiguration : IEntityTypeConfiguration<Anamnese>
{
    public void Configure(EntityTypeBuilder<Anamnese> builder)
    {
        builder.ToTable("anamnese");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SolicitacaoExameId).HasColumnName("solicitacao_exame_id").IsRequired();

        builder.Property(a => a.Tipo).HasColumnName("tipo").HasMaxLength(40).IsRequired();
        builder.Property(a => a.Versao).HasColumnName("versao").HasDefaultValue(1).IsRequired();
        builder.Property(a => a.ConteudoJson).HasColumnName("conteudo_json").HasColumnType("jsonb").IsRequired();
        builder.Property(a => a.ClassificacaoRisco).HasColumnName("classificacao_risco").HasMaxLength(10);

        builder.Property(a => a.PreenchidoPorUsuarioId).HasColumnName("preenchido_por_usuario_id");
        builder.Property(a => a.PreenchidoPorNome).HasColumnName("preenchido_por_nome").HasMaxLength(200);

        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(a => a.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(a => a.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(a => a.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(a => a.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(a => a.SolicitacaoExame)
            .WithMany()
            .HasForeignKey(a => a.SolicitacaoExameId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1 anamnese ativa por solicitação (soft-delete libera o slot).
        builder.HasIndex(a => a.SolicitacaoExameId)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_anamnese_solicitacao_exame_id_ativa");

        builder.HasIndex(a => a.ClassificacaoRisco);
    }
}
