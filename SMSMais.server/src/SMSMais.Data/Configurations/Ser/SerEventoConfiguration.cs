using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Data.Configurations.Ser;

internal sealed class SerEventoConfiguration : IEntityTypeConfiguration<SerEvento>
{
    public void Configure(EntityTypeBuilder<SerEvento> builder)
    {
        builder.ToTable("ser_evento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SerSolicitacaoId).HasColumnName("ser_solicitacao_id").IsRequired();
        builder.Property(x => x.DataEvento).HasColumnName("data_evento").IsRequired();
        builder.Property(x => x.Evento).HasColumnName("evento").HasMaxLength(60).IsRequired();
        builder.Property(x => x.EstadoAnterior).HasColumnName("estado_anterior").HasMaxLength(60);
        builder.Property(x => x.EstadoAtual).HasColumnName("estado_atual").HasMaxLength(60);
        builder.Property(x => x.CentralRegulacao).HasColumnName("central_regulacao").HasMaxLength(150);
        builder.Property(x => x.UnidadeExecutora).HasColumnName("unidade_executora").HasMaxLength(200);
        builder.Property(x => x.Usuario).HasColumnName("usuario").HasMaxLength(200);
        builder.Property(x => x.LotacaoEvento).HasColumnName("lotacao_evento").HasMaxLength(200);
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(45);
        builder.Property(x => x.Observacao).HasColumnName("observacao");
        builder.Property(x => x.CapturadoEm).HasColumnName("capturado_em").IsRequired();

        // IDEMPOTÊNCIA DO RE-SCRAPING. O SER não numera os eventos, então a identidade é a
        // tripla (solicitação, instante, verbo). Sem isto, reler o histórico todo dia — o que
        // é obrigatório para pegar FollowUP — duplicaria a trilha inteira a cada rodada.
        builder.HasIndex(x => new { x.SerSolicitacaoId, x.DataEvento, x.Evento })
            .IsUnique()
            .HasDatabaseName("ux_ser_evento_solicitacao_data_evento");

        // A tela do detalhe lista a trilha do mais recente para o mais antigo.
        builder.HasIndex(x => new { x.SerSolicitacaoId, x.DataEvento })
            .HasDatabaseName("ix_ser_evento_solicitacao_data")
            .IsDescending(false, true);

        // "Quais FollowUPs entraram hoje" — a consulta do gatilho.
        builder.HasIndex(x => new { x.Evento, x.CapturadoEm })
            .HasDatabaseName("ix_ser_evento_evento_capturado");
    }
}
