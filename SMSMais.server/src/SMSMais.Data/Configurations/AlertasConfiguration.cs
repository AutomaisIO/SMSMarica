using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Alertas;

namespace SMSMais.Data.Configurations;

internal sealed class AlertaDestinatarioConfiguration : IEntityTypeConfiguration<AlertaDestinatario>
{
    public void Configure(EntityTypeBuilder<AlertaDestinatario> builder)
    {
        builder.ToTable("alerta_destinatario");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Telefone).HasColumnName("telefone").HasMaxLength(13).IsRequired();
        builder.Property(e => e.Nome).HasColumnName("nome").HasMaxLength(120);
        builder.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);

        builder.HasIndex(e => e.Telefone).IsUnique().HasDatabaseName("ux_alerta_destinatario_telefone");
    }
}

internal sealed class AlertaOrigemConfiguration : IEntityTypeConfiguration<AlertaOrigem>
{
    public void Configure(EntityTypeBuilder<AlertaOrigem> builder)
    {
        builder.ToTable("alerta_origem");
        builder.HasKey(e => e.Chave);

        builder.Property(e => e.Chave).HasColumnName("chave").HasMaxLength(200);
        builder.Property(e => e.Rotulo).HasColumnName("rotulo").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Grupo).HasColumnName("grupo").HasMaxLength(60).IsRequired();
        builder.Property(e => e.Silenciada).HasColumnName("silenciada").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CriadaEm).HasColumnName("criada_em").IsRequired();
        builder.Property(e => e.UltimaOcorrenciaEm).HasColumnName("ultima_ocorrencia_em");
        builder.Property(e => e.Ocorrencias).HasColumnName("ocorrencias").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.OcorrenciasSemAviso).HasColumnName("ocorrencias_sem_aviso").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.UltimoAvisoEm).HasColumnName("ultimo_aviso_em");
        builder.Property(e => e.AvisosSeguidos).HasColumnName("avisos_seguidos").HasDefaultValue(0).IsRequired();
        builder.Property(e => e.UltimoTitulo).HasColumnName("ultimo_titulo").HasMaxLength(300);
        builder.Property(e => e.UltimoDetalhe).HasColumnName("ultimo_detalhe").HasColumnType("text");
        builder.Property(e => e.AtualizadaPor).HasColumnName("atualizada_por").HasMaxLength(200);
    }
}

internal sealed class AlertaEnvioConfiguration : IEntityTypeConfiguration<AlertaEnvio>
{
    public void Configure(EntityTypeBuilder<AlertaEnvio> builder)
    {
        builder.ToTable("alerta_envio");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.OrigemChave).HasColumnName("origem_chave").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Titulo).HasColumnName("titulo").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Detalhe).HasColumnName("detalhe").HasColumnType("text").IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.Ocorrencias).HasColumnName("ocorrencias").HasDefaultValue(1).IsRequired();
        builder.Property(e => e.Situacao).HasColumnName("situacao").IsRequired();
        builder.Property(e => e.Resultado).HasColumnName("resultado").HasColumnType("text");

        builder.HasIndex(e => e.CriadoEm).HasDatabaseName("ix_alerta_envio_criado_em");
        builder.HasIndex(e => new { e.OrigemChave, e.CriadoEm }).HasDatabaseName("ix_alerta_envio_origem");
    }
}
