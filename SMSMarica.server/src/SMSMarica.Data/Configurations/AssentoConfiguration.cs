using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class AssentoConfiguration : IEntityTypeConfiguration<Assento>
{
    public void Configure(EntityTypeBuilder<Assento> builder)
    {
        builder.ToTable("veiculo_assento");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.FileiraId).HasColumnName("fileira_id").IsRequired();
        builder.Property(a => a.Numero).HasColumnName("numero").IsRequired();
        builder.Property(a => a.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(a => new { a.FileiraId, a.Numero }).IsUnique();
    }
}
