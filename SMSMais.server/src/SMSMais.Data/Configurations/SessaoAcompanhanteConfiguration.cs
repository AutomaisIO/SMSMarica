using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class SessaoAcompanhanteConfiguration : IEntityTypeConfiguration<SessaoAcompanhante>
{
    public void Configure(EntityTypeBuilder<SessaoAcompanhante> builder)
    {
        builder.ToTable("sessao_acompanhante");
        builder.HasKey(x => new { x.SessaoId, x.AcompanhanteId });

        builder.Property(x => x.SessaoId).HasColumnName("sessao_id");
        builder.Property(x => x.AcompanhanteId).HasColumnName("acompanhante_id");

        builder.HasOne(x => x.Sessao)
            .WithMany(s => s.Acompanhantes)
            .HasForeignKey(x => x.SessaoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Acompanhante sai por exclusão lógica; a trava impede apagar o histórico de quem viajou.
        builder.HasOne(x => x.Acompanhante)
            .WithMany()
            .HasForeignKey(x => x.AcompanhanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AcompanhanteId);
    }
}
