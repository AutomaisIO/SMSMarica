using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class EventoViagemConfiguration : IEntityTypeConfiguration<EventoViagem>
{
    public void Configure(EntityTypeBuilder<EventoViagem> builder)
    {
        builder.ToTable("translado_evento_viagem");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.IdLocal).HasColumnName("id_local").IsRequired();
        builder.Property(e => e.RotaId).HasColumnName("rota_id").IsRequired();
        builder.Property(e => e.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(e => e.DispositivoId).HasColumnName("dispositivo_id").IsRequired();
        builder.Property(e => e.MotoristaId).HasColumnName("motorista_id");
        builder.Property(e => e.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(e => e.SessaoId).HasColumnName("sessao_id");
        builder.Property(e => e.Pessoa).HasColumnName("pessoa").HasConversion<int?>();
        builder.Property(e => e.AcompanhanteOrdem).HasColumnName("acompanhante_ordem");
        builder.Property(e => e.AcompanhanteId).HasColumnName("acompanhante_id");
        builder.Property(e => e.Resgate).HasColumnName("resgate").IsRequired();
        builder.Property(e => e.Latitude).HasColumnName("latitude");
        builder.Property(e => e.Longitude).HasColumnName("longitude");
        builder.Property(e => e.PrecisaoM).HasColumnName("precisao_m");
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(e => e.RecebidoEm).HasColumnName("recebido_em").IsRequired();
        builder.Property(e => e.Aplicado).HasColumnName("aplicado").IsRequired();
        builder.Property(e => e.MotivoNaoAplicado).HasColumnName("motivo_nao_aplicado").HasMaxLength(200);

        // A rota só é apagada enquanto planejada (gerar de novo), e aí não tem viagem.
        builder.HasOne<RotaDiaria>().WithMany().HasForeignKey(e => e.RotaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Veiculo>().WithMany().HasForeignKey(e => e.VeiculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Motorista>().WithMany().HasForeignKey(e => e.MotoristaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<SessaoDeTratamento>().WithMany().HasForeignKey(e => e.SessaoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Acompanhante>().WithMany().HasForeignKey(e => e.AcompanhanteId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.IdLocal).IsUnique();
        // A viagem de uma rota em ordem, e o histórico de uma sessão.
        builder.HasIndex(e => new { e.RotaId, e.OcorridoEm });
        builder.HasIndex(e => e.SessaoId);
    }
}
