using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Conversas;

namespace SMSMarica.Data.Configurations;

internal sealed class ConversaEventoConfiguration : IEntityTypeConfiguration<ConversaEvento>
{
    public void Configure(EntityTypeBuilder<ConversaEvento> builder)
    {
        builder.ToTable("conversa_evento");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ConversaId).HasColumnName("conversa_id").IsRequired();
        builder.Property(e => e.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(e => e.AtorUsuarioId).HasColumnName("ator_usuario_id");
        builder.Property(e => e.DeUsuarioId).HasColumnName("de_usuario_id");
        builder.Property(e => e.ParaUsuarioId).HasColumnName("para_usuario_id");
        builder.Property(e => e.DeUnidadeId).HasColumnName("de_unidade_id");
        builder.Property(e => e.ParaUnidadeId).HasColumnName("para_unidade_id");
        builder.Property(e => e.Observacao).HasColumnName("observacao").HasMaxLength(1000);
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasOne(e => e.Conversa)
            .WithMany(c => c.Eventos)
            .HasForeignKey(e => e.ConversaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ConversaId, e.OcorridoEm });
    }
}
