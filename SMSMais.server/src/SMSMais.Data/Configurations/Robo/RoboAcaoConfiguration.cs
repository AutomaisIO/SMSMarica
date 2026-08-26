using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class RoboAcaoConfiguration : IEntityTypeConfiguration<RoboAcao>
{
    public void Configure(EntityTypeBuilder<RoboAcao> builder)
    {
        builder.ToTable("robo_acao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ConversaId).HasColumnName("conversa_id").IsRequired();
        builder.Property(a => a.RoboAssuntoId).HasColumnName("robo_assunto_id");
        builder.Property(a => a.Comando).HasColumnName("comando").HasConversion<int>().IsRequired();
        builder.Property(a => a.EntradaJson).HasColumnName("entrada_json").HasColumnType("jsonb");
        builder.Property(a => a.ResultadoJson).HasColumnName("resultado_json").HasColumnType("jsonb");
        builder.Property(a => a.Sucesso).HasColumnName("sucesso").IsRequired();
        builder.Property(a => a.IdempotenciaChave).HasColumnName("idempotencia_chave").HasMaxLength(200).IsRequired();
        builder.Property(a => a.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();

        builder.HasOne(a => a.Conversa)
            .WithMany()
            .HasForeignKey(a => a.ConversaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.RoboAssunto)
            .WithMany()
            .HasForeignKey(a => a.RoboAssuntoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Cada ação aplicada uma única vez.
        builder.HasIndex(a => a.IdempotenciaChave)
            .HasDatabaseName("ux_robo_acao_idempotencia")
            .IsUnique();

        builder.HasIndex(a => a.ConversaId)
            .HasDatabaseName("ix_robo_acao_conversa");
    }
}
