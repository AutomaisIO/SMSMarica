using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class LaudoConfiguracaoConfiguration : IEntityTypeConfiguration<LaudoConfiguracao>
{
    public void Configure(EntityTypeBuilder<LaudoConfiguracao> builder)
    {
        builder.ToTable("laudo_configuracao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.CabecalhoJson).HasColumnName("cabecalho_json").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.CabecalhoHtml).HasColumnName("cabecalho_html").HasColumnType("text").IsRequired();
        builder.Property(c => c.RodapeJson).HasColumnName("rodape_json").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.RodapeHtml).HasColumnName("rodape_html").HasColumnType("text").IsRequired();

        builder.Property(c => c.PermitirLaudarSemAssociacao)
            .HasColumnName("permitir_laudar_sem_associacao").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.PermitirLaudarSemAnamnese)
            .HasColumnName("permitir_laudar_sem_anamnese").HasDefaultValue(false).IsRequired();

        builder.Property(c => c.DownloadLinkValidadeDias)
            .HasColumnName("download_link_validade_dias").HasDefaultValue(7).IsRequired();

        builder.Property(c => c.AtualizadoPorUsuarioId).HasColumnName("atualizado_por_usuario_id");
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(c => c.AtualizadoPorUsuario)
            .WithMany()
            .HasForeignKey(c => c.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
