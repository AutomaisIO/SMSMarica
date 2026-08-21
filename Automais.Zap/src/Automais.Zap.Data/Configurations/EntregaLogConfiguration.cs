using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class EntregaLogConfiguration : IEntityTypeConfiguration<EntregaLog>
{
    public void Configure(EntityTypeBuilder<EntregaLog> b)
    {
        b.ToTable("entrega_log");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.PhoneNumberId).HasColumnName("phone_number_id").HasMaxLength(200).IsRequired();
        b.Property(x => x.DestinoId).HasColumnName("destino_id");
        b.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(120).IsRequired();
        b.Property(x => x.Sucesso).HasColumnName("sucesso");
        b.Property(x => x.StatusHttp).HasColumnName("status_http");
        b.Property(x => x.DuracaoMs).HasColumnName("duracao_ms");
        b.Property(x => x.Erro).HasColumnName("erro").HasMaxLength(500);
        b.Property(x => x.RecebidoEm).HasColumnName("recebido_em");

        b.HasIndex(x => x.RecebidoEm).HasDatabaseName("ix_entrega_log_recebido_em");
        b.HasIndex(x => new { x.DestinoId, x.RecebidoEm }).HasDatabaseName("ix_entrega_log_destino_recebido");
    }
}
