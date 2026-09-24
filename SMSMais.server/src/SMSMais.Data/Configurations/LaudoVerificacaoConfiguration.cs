using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class LaudoVerificacaoConfiguration : IEntityTypeConfiguration<LaudoVerificacao>
{
    public void Configure(EntityTypeBuilder<LaudoVerificacao> builder)
    {
        builder.ToTable("laudo_verificacao");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.LaudoId).HasColumnName("laudo_id").IsRequired();
        builder.Property(v => v.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Um selo por laudo (estável entre re-assinaturas).
        builder.HasIndex(v => v.LaudoId).IsUnique();

        // Restrict: laudo é append-only e nunca é apagado fisicamente; se um dia for, o selo
        // não pode sumir calado junto.
        builder.HasOne(v => v.Laudo)
            .WithMany()
            .HasForeignKey(v => v.LaudoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
