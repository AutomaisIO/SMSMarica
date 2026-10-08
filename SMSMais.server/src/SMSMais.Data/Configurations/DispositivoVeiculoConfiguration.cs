using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class DispositivoVeiculoConfiguration : IEntityTypeConfiguration<DispositivoVeiculo>
{
    public void Configure(EntityTypeBuilder<DispositivoVeiculo> builder)
    {
        builder.ToTable("veiculo_dispositivo");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(d => d.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(d => d.CodigoHash).HasColumnName("codigo_hash").HasMaxLength(64);
        builder.Property(d => d.CodigoExpiraEm).HasColumnName("codigo_expira_em");
        builder.Property(d => d.Modelo).HasColumnName("modelo").HasMaxLength(120);
        builder.Property(d => d.Identificador).HasColumnName("identificador").HasMaxLength(120);
        builder.Property(d => d.AtivadoEm).HasColumnName("ativado_em");
        builder.Property(d => d.UltimoContatoEm).HasColumnName("ultimo_contato_em");
        builder.Property(d => d.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(d => d.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(d => d.Veiculo).WithMany().HasForeignKey(d => d.VeiculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.VeiculoId).IsUnique();
        builder.HasIndex(d => d.TokenHash).IsUnique();
        builder.HasIndex(d => d.CodigoHash);
    }
}
