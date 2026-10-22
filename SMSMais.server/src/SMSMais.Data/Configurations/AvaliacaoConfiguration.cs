using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("avaliacao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SessaoId).HasColumnName("sessao_id").IsRequired();
        builder.Property(a => a.Nota).HasColumnName("nota").IsRequired();
        builder.Property(a => a.Comentario).HasColumnName("comentario").HasMaxLength(2000);
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.Canal).HasColumnName("canal").HasConversion<int>().IsRequired()
            .HasDefaultValue(Entities.Enums.CanalAvaliacao.Painel).HasSentinel((Entities.Enums.CanalAvaliacao)0);
        builder.Property(a => a.MotoristaId).HasColumnName("motorista_id");
        builder.Property(a => a.VeiculoId).HasColumnName("veiculo_id");
        builder.Property(a => a.RotaId).HasColumnName("rota_id");
        builder.Property(a => a.Trecho).HasColumnName("trecho").HasConversion<int>().IsRequired()
            .HasDefaultValue(Entities.Enums.TrechoViagem.Ida).HasSentinel((Entities.Enums.TrechoViagem)0);

        builder.HasOne(a => a.Sessao).WithMany().HasForeignKey(a => a.SessaoId).OnDelete(DeleteBehavior.Cascade);
        // Uma nota por trecho da viagem: a da ida e a do resgate.
        builder.HasIndex(a => new { a.SessaoId, a.Trecho }).IsUnique();
        builder.HasOne<Motorista>().WithMany().HasForeignKey(a => a.MotoristaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Veiculo>().WithMany().HasForeignKey(a => a.VeiculoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<RotaDiaria>().WithMany().HasForeignKey(a => a.RotaId).OnDelete(DeleteBehavior.SetNull);
        // Notas por motorista e por veículo no painel.
        builder.HasIndex(a => a.MotoristaId);
        builder.HasIndex(a => a.VeiculoId);
    }
}
