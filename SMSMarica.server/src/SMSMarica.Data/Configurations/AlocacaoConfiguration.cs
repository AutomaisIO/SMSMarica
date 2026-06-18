using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Configurations;

internal sealed class AlocacaoConfiguration : IEntityTypeConfiguration<Alocacao>
{
    public void Configure(EntityTypeBuilder<Alocacao> builder)
    {
        builder.ToTable("translado_alocacao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.RotaDiariaId).HasColumnName("rota_diaria_id").IsRequired();
        builder.Property(a => a.SessaoId).HasColumnName("sessao_id").IsRequired();
        builder.Property(a => a.AssentoId).HasColumnName("assento_id").IsRequired();
        builder.Property(a => a.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.Property(a => a.OrdemParada).HasColumnName("ordem_parada");
        builder.Property(a => a.Parada).HasColumnName("tipo_parada").HasConversion<int>().HasDefaultValue(TipoParada.Coleta).IsRequired();
        builder.Property(a => a.EtaPrevisto).HasColumnName("eta_previsto");
        builder.Property(a => a.JanelaInicio).HasColumnName("janela_inicio");
        builder.Property(a => a.JanelaFim).HasColumnName("janela_fim");

        builder.HasOne(a => a.Sessao).WithMany().HasForeignKey(a => a.SessaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Assento).WithMany().HasForeignKey(a => a.AssentoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.RotaDiariaId, a.AssentoId }).IsUnique();
        builder.HasIndex(a => new { a.RotaDiariaId, a.OrdemParada });
    }
}
