using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class AtendimentoConfirmacaoConfiguration : IEntityTypeConfiguration<AtendimentoConfirmacao>
{
    public void Configure(EntityTypeBuilder<AtendimentoConfirmacao> builder)
    {
        builder.ToTable("atendimento_confirmacao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(a => a.AtendenteUsuarioId).HasColumnName("atendente_usuario_id").IsRequired();
        builder.Property(a => a.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(a => a.Motivo).HasColumnName("motivo").HasMaxLength(500);
        builder.Property(a => a.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(a => a.EncerradoEm).HasColumnName("encerrado_em");
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(a => a.AtualizadoPor).HasColumnName("atualizado_por");

        builder.Property(a => a.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(a => a.Solicitacao)
            .WithMany()
            .HasForeignKey(a => a.SolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Atendente)
            .WithMany()
            .HasForeignKey(a => a.AtendenteUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // No máximo um atendimento ATIVO por solicitação.
        builder.HasIndex(a => a.SolicitacaoId)
            .IsUnique()
            .HasFilter("encerrado_em IS NULL")
            .HasDatabaseName("ux_atendimento_confirmacao_ativo");

        builder.HasIndex(a => new { a.AtendenteUsuarioId, a.Situacao });
    }
}

internal sealed class AtendimentoConfirmacaoEventoConfiguration : IEntityTypeConfiguration<AtendimentoConfirmacaoEvento>
{
    public void Configure(EntityTypeBuilder<AtendimentoConfirmacaoEvento> builder)
    {
        builder.ToTable("atendimento_confirmacao_evento");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AtendimentoId).HasColumnName("atendimento_id").IsRequired();
        builder.Property(e => e.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(e => e.AtorUsuarioId).HasColumnName("ator_usuario_id");
        builder.Property(e => e.DeUsuarioId).HasColumnName("de_usuario_id");
        builder.Property(e => e.ParaUsuarioId).HasColumnName("para_usuario_id");
        builder.Property(e => e.Observacao).HasColumnName("observacao").HasMaxLength(1000);
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();

        builder.HasOne(e => e.Atendimento)
            .WithMany(a => a.Eventos)
            .HasForeignKey(e => e.AtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.AtendimentoId, e.OcorridoEm });
    }
}
