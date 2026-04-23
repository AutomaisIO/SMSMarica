using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

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

        builder.HasOne(a => a.Sessao).WithMany().HasForeignKey(a => a.SessaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.SessaoId).IsUnique();
    }
}
