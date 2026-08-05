using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Data.Configurations.Ser;

internal sealed class SerGatilhoConfiguration : IEntityTypeConfiguration<SerGatilho>
{
    public void Configure(EntityTypeBuilder<SerGatilho> builder)
    {
        builder.ToTable("ser_gatilho");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SerSolicitacaoId).HasColumnName("ser_solicitacao_id").IsRequired();
        builder.Property(x => x.IdSer).HasColumnName("id_ser").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.ChaveEvento).HasColumnName("chave_evento").HasMaxLength(80).IsRequired();
        builder.Property(x => x.SituacaoAnterior).HasColumnName("situacao_anterior");
        builder.Property(x => x.SituacaoAtual).HasColumnName("situacao_atual");
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.ProcessadoEm).HasColumnName("processado_em");
        builder.Property(x => x.ProcessadoPor).HasColumnName("processado_por").HasMaxLength(120);

        // Idempotência: a mesma ocorrência não vira dois gatilhos entre rodadas. `chave_evento`
        // é o discriminador dentro do tipo (data do evento no FollowUP, situação de destino na
        // mudança de situação).
        builder.HasIndex(x => new { x.SerSolicitacaoId, x.Tipo, x.ChaveEvento })
            .IsUnique()
            .HasDatabaseName("ux_ser_gatilho_solicitacao_tipo_chave");

        // A consulta do consumidor futuro: "o que está na fila, mais antigo primeiro".
        // Índice PARCIAL — a fila é sempre o subconjunto não processado, e ela tende a ficar
        // pequena enquanto a tabela cresce indefinidamente.
        builder.HasIndex(x => new { x.Tipo, x.CriadoEm })
            .HasDatabaseName("ix_ser_gatilho_pendente")
            .HasFilter("processado_em IS NULL");

        builder.HasOne(x => x.SerSolicitacao)
            .WithMany()
            .HasForeignKey(x => x.SerSolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
