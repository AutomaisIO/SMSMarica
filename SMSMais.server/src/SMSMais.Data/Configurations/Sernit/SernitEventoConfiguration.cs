using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Data.Configurations.Sernit;

internal sealed class SernitEventoConfiguration : IEntityTypeConfiguration<SernitEvento>
{
    public void Configure(EntityTypeBuilder<SernitEvento> builder)
    {
        builder.ToTable("sernit_evento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SernitSolicitacaoId).HasColumnName("sernit_solicitacao_id").IsRequired();
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

        // IDEMPOTÊNCIA DO RE-SCRAPING — o SERNIT não numera eventos; a identidade é a tripla
        // (solicitação, instante, verbo). Reler o histórico todo dia (para pegar FollowUP) só
        // insere o que é novo.
        builder.HasIndex(x => new { x.SernitSolicitacaoId, x.DataEvento, x.Evento })
            .IsUnique()
            .HasDatabaseName("ux_sernit_evento_solicitacao_data_evento");

        builder.HasIndex(x => new { x.SernitSolicitacaoId, x.DataEvento })
            .HasDatabaseName("ix_sernit_evento_solicitacao_data")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.Evento, x.CapturadoEm })
            .HasDatabaseName("ix_sernit_evento_evento_capturado");
    }
}
