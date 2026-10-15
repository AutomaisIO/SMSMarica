using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

// Login do motorista no tablet pelo rosto (ADR-0073).

internal sealed class MotoristaReferenciaFacialConfiguration : IEntityTypeConfiguration<MotoristaReferenciaFacial>
{
    public void Configure(EntityTypeBuilder<MotoristaReferenciaFacial> builder)
    {
        builder.ToTable("motorista_referencia_facial");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.MotoristaId).HasColumnName("motorista_id").IsRequired();
        builder.Property(r => r.FotoBase64).HasColumnName("foto_base64").IsRequired();
        builder.Property(r => r.FotoHash).HasColumnName("foto_hash").HasMaxLength(64).IsRequired();
        builder.Property(r => r.DispositivoId).HasColumnName("dispositivo_id");
        builder.Property(r => r.VeiculoId).HasColumnName("veiculo_id");
        builder.Property(r => r.TermoVersao).HasColumnName("termo_versao").HasMaxLength(32).IsRequired();
        builder.Property(r => r.TermoHash).HasColumnName("termo_hash").HasMaxLength(64).IsRequired();
        builder.Property(r => r.CadastradaEm).HasColumnName("cadastrada_em").IsRequired();

        builder.HasOne<Motorista>().WithMany().HasForeignKey(r => r.MotoristaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Veiculo>().WithMany().HasForeignKey(r => r.VeiculoId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.MotoristaId).IsUnique();
    }
}

internal sealed class VerificacaoFacialMotoristaConfiguration : IEntityTypeConfiguration<VerificacaoFacialMotorista>
{
    public void Configure(EntityTypeBuilder<VerificacaoFacialMotorista> builder)
    {
        builder.ToTable("rastreamento_verificacao_facial");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.IdLocal).HasColumnName("id_local").IsRequired();
        builder.Property(v => v.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(v => v.DispositivoId).HasColumnName("dispositivo_id").IsRequired();
        builder.Property(v => v.MotoristaId).HasColumnName("motorista_id");
        builder.Property(v => v.RotaId).HasColumnName("rota_id");
        builder.Property(v => v.Momento).HasColumnName("momento").HasConversion<int>().IsRequired();
        builder.Property(v => v.Meio).HasColumnName("meio").HasConversion<int>().IsRequired();
        builder.Property(v => v.Aprovada).HasColumnName("aprovada").IsRequired();
        builder.Property(v => v.Nota).HasColumnName("nota");
        builder.Property(v => v.Limiar).HasColumnName("limiar");
        builder.Property(v => v.ProvaDeVida).HasColumnName("prova_de_vida").IsRequired();
        builder.Property(v => v.ModeloVersao).HasColumnName("modelo_versao").HasMaxLength(64);
        builder.Property(v => v.Latitude).HasColumnName("latitude");
        builder.Property(v => v.Longitude).HasColumnName("longitude");
        builder.Property(v => v.PrecisaoM).HasColumnName("precisao_m");
        builder.Property(v => v.CapturadaEm).HasColumnName("capturada_em").IsRequired();
        builder.Property(v => v.RecebidaEm).HasColumnName("recebida_em").IsRequired();
        builder.Property(v => v.SelfieBase64).HasColumnName("selfie_base64");
        builder.Property(v => v.SelfieApagadaEm).HasColumnName("selfie_apagada_em");
        builder.Property(v => v.LiberacaoId).HasColumnName("liberacao_id");

        builder.HasOne<Veiculo>().WithMany().HasForeignKey(v => v.VeiculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Motorista>().WithMany().HasForeignKey(v => v.MotoristaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<RotaDiaria>().WithMany().HasForeignKey(v => v.RotaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<LiberacaoMotorista>().WithMany().HasForeignKey(v => v.LiberacaoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(v => v.IdLocal).IsUnique();
        // Auditoria por veículo e por motorista, mais recentes primeiro.
        builder.HasIndex(v => new { v.VeiculoId, v.RecebidaEm });
        builder.HasIndex(v => new { v.MotoristaId, v.RecebidaEm });
    }
}

internal sealed class LiberacaoMotoristaConfiguration : IEntityTypeConfiguration<LiberacaoMotorista>
{
    public void Configure(EntityTypeBuilder<LiberacaoMotorista> builder)
    {
        builder.ToTable("rastreamento_liberacao_motorista");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(l => l.MotoristaId).HasColumnName("motorista_id").IsRequired();
        builder.Property(l => l.CodigoHash).HasColumnName("codigo_hash").HasMaxLength(64).IsRequired();
        builder.Property(l => l.CriadaEm).HasColumnName("criada_em").IsRequired();
        builder.Property(l => l.CriadaPor).HasColumnName("criada_por");
        builder.Property(l => l.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(l => l.TentativasErradas).HasColumnName("tentativas_erradas").IsRequired();
        builder.Property(l => l.UsadaEm).HasColumnName("usada_em");
        builder.Property(l => l.UsadaPorDispositivoId).HasColumnName("usada_por_dispositivo_id");

        builder.HasOne<Veiculo>().WithMany().HasForeignKey(l => l.VeiculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Motorista>().WithMany().HasForeignKey(l => l.MotoristaId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.VeiculoId, l.CriadaEm });
    }
}
