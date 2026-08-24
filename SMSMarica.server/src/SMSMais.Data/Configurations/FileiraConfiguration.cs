using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class FileiraConfiguration : IEntityTypeConfiguration<Fileira>
{
    public void Configure(EntityTypeBuilder<Fileira> builder)
    {
        builder.ToTable("veiculo_fileira");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.VeiculoId).HasColumnName("veiculo_id").IsRequired();
        builder.Property(f => f.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(f => f.QuantidadeAssentos).HasColumnName("quantidade_assentos").IsRequired();
        builder.Property(f => f.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(f => new { f.VeiculoId, f.Ordem }).IsUnique();

        builder.HasMany(f => f.Assentos)
            .WithOne(a => a.Fileira)
            .HasForeignKey(a => a.FileiraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
