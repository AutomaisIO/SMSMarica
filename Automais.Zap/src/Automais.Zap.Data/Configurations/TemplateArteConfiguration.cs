using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class TemplateArteConfiguration : IEntityTypeConfiguration<TemplateArte>
{
    public void Configure(EntityTypeBuilder<TemplateArte> b)
    {
        b.ToTable("template_arte");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.WabaId).HasColumnName("waba_id");
        b.Property(x => x.Template).HasColumnName("template").HasMaxLength(512).IsRequired();
        b.Property(x => x.MidiaId).HasColumnName("midia_id");
        b.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        b.Property(x => x.AtualizadoPorUsuarioId).HasColumnName("atualizado_por_usuario_id");

        // Um modelo tem UMA arte. O nome é único dentro do WABA na própria Meta.
        b.HasIndex(x => new { x.WabaId, x.Template })
            .IsUnique().HasDatabaseName("ux_template_arte_waba_template");

        b.HasOne(x => x.Waba).WithMany()
            .HasForeignKey(x => x.WabaId).OnDelete(DeleteBehavior.Cascade);

        // Restrict de propósito: apagar uma mídia ainda escolhida por um modelo quebraria o
        // envio daquele modelo em silêncio. O painel manda tirar a escolha primeiro.
        b.HasOne(x => x.Midia).WithMany()
            .HasForeignKey(x => x.MidiaId).OnDelete(DeleteBehavior.Restrict);
    }
}
