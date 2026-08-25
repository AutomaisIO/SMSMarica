using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Data.Configurations.Sernit;

internal sealed class SernitGatilhoConfiguration : IEntityTypeConfiguration<SernitGatilho>
{
    public void Configure(EntityTypeBuilder<SernitGatilho> builder)
    {
        builder.ToTable("sernit_gatilho");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SernitSolicitacaoId).HasColumnName("sernit_solicitacao_id").IsRequired();
        builder.Property(x => x.IdSernit).HasColumnName("id_sernit").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.ChaveEvento).HasColumnName("chave_evento").HasMaxLength(80).IsRequired();
        builder.Property(x => x.SituacaoAnterior).HasColumnName("situacao_anterior");
        builder.Property(x => x.SituacaoAtual).HasColumnName("situacao_atual");
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.ProcessadoEm).HasColumnName("processado_em");
        builder.Property(x => x.ProcessadoPor).HasColumnName("processado_por").HasMaxLength(120);

        builder.HasIndex(x => new { x.SernitSolicitacaoId, x.Tipo, x.ChaveEvento })
            .IsUnique()
            .HasDatabaseName("ux_sernit_gatilho_solicitacao_tipo_chave");

        builder.HasIndex(x => new { x.Tipo, x.CriadoEm })
            .HasDatabaseName("ix_sernit_gatilho_pendente")
            .HasFilter("processado_em IS NULL");

        builder.HasOne(x => x.SernitSolicitacao)
            .WithMany()
            .HasForeignKey(x => x.SernitSolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
