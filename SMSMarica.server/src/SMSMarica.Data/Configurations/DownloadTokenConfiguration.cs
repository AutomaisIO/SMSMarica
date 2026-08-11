using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class DownloadTokenConfiguration : IEntityTypeConfiguration<DownloadToken>
{
    public void Configure(EntityTypeBuilder<DownloadToken> builder)
    {
        builder.ToTable("download_token");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Tipo).HasColumnName("tipo").HasMaxLength(40).IsRequired();
        builder.Property(t => t.ReferenciaId).HasColumnName("referencia_id").IsRequired();
        builder.Property(t => t.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(t => t.TentativasCpf)
            .HasColumnName("tentativas_cpf").HasDefaultValue(0).IsRequired();
        builder.Property(t => t.Liberacao).HasColumnName("liberacao");
        builder.Property(t => t.LiberadoEm).HasColumnName("liberado_em");
        builder.Property(t => t.UsadoEm).HasColumnName("usado_em");
        builder.Property(t => t.UsadoIp).HasColumnName("usado_ip").HasMaxLength(64);
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(t => new { t.Tipo, t.ReferenciaId });
    }
}
