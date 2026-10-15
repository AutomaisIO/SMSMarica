using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class OcorrenciaVeiculoConfiguration : IEntityTypeConfiguration<OcorrenciaVeiculo>
{
    public const int TamanhoDescricao = 500;
    public const int TamanhoObservacao = 1000;

    public void Configure(EntityTypeBuilder<OcorrenciaVeiculo> builder)
    {
        builder.ToTable("rastreamento_ocorrencia");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(o => o.DispositivoId).HasColumnName("dispositivo_id").IsRequired();
        builder.Property(o => o.IdLocal).HasColumnName("id_local").IsRequired();
        builder.Property(o => o.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(o => o.Descricao).HasColumnName("descricao").HasMaxLength(TamanhoDescricao);
        builder.Property(o => o.Latitude).HasColumnName("latitude");
        builder.Property(o => o.Longitude).HasColumnName("longitude");
        builder.Property(o => o.PrecisaoM).HasColumnName("precisao_m");
        builder.Property(o => o.RotaId).HasColumnName("rota_id");
        builder.Property(o => o.OcorridaEm).HasColumnName("ocorrida_em").IsRequired();
        builder.Property(o => o.RecebidaEm).HasColumnName("recebida_em").IsRequired();
        builder.Property(o => o.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(o => o.AtendidaEm).HasColumnName("atendida_em");
        builder.Property(o => o.AtendidaPor).HasColumnName("atendida_por");
        builder.Property(o => o.EncerradaEm).HasColumnName("encerrada_em");
        builder.Property(o => o.EncerradaPor).HasColumnName("encerrada_por");
        builder.Property(o => o.ObservacaoCentral).HasColumnName("observacao_central").HasMaxLength(TamanhoObservacao);
        builder.Property(o => o.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(o => o.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne<Veiculo>().WithMany().HasForeignKey(o => o.VeiculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RotaDiaria>().WithMany().HasForeignKey(o => o.RotaId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(o => o.IdLocal).IsUnique();
        // Lista da central: abertas primeiro, mais recentes primeiro.
        builder.HasIndex(o => new { o.Situacao, o.RecebidaEm });
    }
}
