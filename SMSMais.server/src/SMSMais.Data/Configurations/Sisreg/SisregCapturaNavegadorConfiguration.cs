using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregCapturaNavegadorConfiguration : IEntityTypeConfiguration<SisregCapturaNavegador>
{
    public void Configure(EntityTypeBuilder<SisregCapturaNavegador> builder)
    {
        builder.ToTable("sisreg_captura_navegador");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.OcorridoEm).HasColumnName("ocorrido_em");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.InstallId).HasColumnName("install_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Versao).HasColumnName("versao").HasMaxLength(20);
        builder.Property(x => x.OperadorSisreg).HasColumnName("operador_sisreg").HasMaxLength(200);
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Metodo).HasColumnName("metodo").HasMaxLength(10);
        builder.Property(x => x.Caminho).HasColumnName("caminho").HasMaxLength(300);
        builder.Property(x => x.Etapa).HasColumnName("etapa").HasMaxLength(80);
        builder.Property(x => x.Evento).HasColumnName("evento").HasMaxLength(60);
        builder.Property(x => x.Escrita).HasColumnName("escrita").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40);
        builder.Property(x => x.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Conteudo).HasColumnName("conteudo").HasColumnType("text");
        builder.Property(x => x.ProcessadoEm).HasColumnName("processado_em");

        // Consultas típicas da análise: por PC/instalação e por tipo de evento, no tempo.
        builder.HasIndex(x => x.CriadoEm).HasDatabaseName("ix_sisreg_captura_criado");
        builder.HasIndex(x => new { x.InstallId, x.CriadoEm }).HasDatabaseName("ix_sisreg_captura_install");
        builder.HasIndex(x => x.Evento)
            .HasDatabaseName("ix_sisreg_captura_evento")
            .HasFilter("evento IS NOT NULL");
    }
}
